using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.NPCs.Abomanation_Small;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// The Flesh Maiden: Visus, grown over. Made at a Demon or Crimson Altar from Visus and 13
    /// Essence of the Fouled, so after Amalgaermis and before the Wall.
    ///
    /// Left click is a combo, one step a click (no auto reuse), each step the item's use time
    /// (40 ticks, less with melee speed): a swing down, a swing up, then a thrust that reaches
    /// 10 tiles (FleshMaidenStrike, ai[0] the step). Unlike Visus the combo never moves its
    /// user. It goes back to the swing down after a second without a strike (FleshMaidenState).
    ///
    /// Right click brings it down from overhead (FleshMaidenSmash): 2.5 times the damage, 50
    /// ticks, and the next one not until a second after it ends. Where it lands, it throws its
    /// wielder back 10 tiles, 2 tiles up, bleeding (FleshMaidenState.Throw).
    ///
    /// The item hits nothing and is never drawn in use (noMelee, noUseGraphic): the projectiles
    /// are the weapon, and VisusLayer draws it in the hands (PolearmArt.FleshMaiden), from its
    /// level or its diagonal sheet. The layered sources of its art are in Tools/Art/FleshMaiden.
    /// </summary>
    public class FleshMaiden : ModItem
    {
        public const int Damage = 47;
        public const int UseTicks = 40;
        public const float Knockback = 6f;

        /// <summary>The smash from overhead: how long it takes, how much harder it hits and throws.</summary>
        public const int SmashTicks = 50;
        public const float SmashDamage = 2.5f;
        public const float SmashKnockback = 1.5f;

        /// <summary>How long after a smash ends before the next can be struck: a second.</summary>
        public const int RestTicks = 60;

        /// <summary>How much Essence of the Fouled goes into it, with Visus.</summary>
        public const int Essence = 13;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 96;
            Item.height = 96;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<FleshMaidenStrike>();
            Item.shootSpeed = 1f;

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override bool AltFunctionUse(Player player) => true;

        /// <summary>
        /// One at a time: not while a strike or a smash is still out (a smash outlasts its use
        /// time by the ticks it stood still on what it hit). The smash not until the last one
        /// has rested. altFunctionUse is already 2 here on a right click.
        /// </summary>
        public override bool CanUseItem(Player player)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<FleshMaidenStrike>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<FleshMaidenSmash>()] > 0)
                return false;

            return player.altFunctionUse != 2 || player.GetModPlayer<FleshMaidenState>().Rest <= 0;
        }

        // The smash takes SmashTicks where a step of the combo takes UseTicks: 40 x 1.25 is exactly
        // 50 at base speed, and melee speed shortens both alike.
        public override float UseTimeMultiplier(Player player) => player.altFunctionUse == 2 ? SmashTicks / (float)UseTicks : 1f;

        public override float UseAnimationMultiplier(Player player) => player.altFunctionUse == 2 ? SmashTicks / (float)UseTicks : 1f;

        /// <summary>
        /// Sends out the next step of the combo, or the smash, with how long the use lasts
        /// (ai[1]), so either runs exactly as long as the item is in use, melee speed and all.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            float duration = Math.Max(1, player.itemAnimationMax);
            FleshMaidenState state = player.GetModPlayer<FleshMaidenState>();

            if (player.altFunctionUse == 2)
            {
                float facing = velocity.X < 0f ? -1f : velocity.X > 0f ? 1f : player.direction;

                Projectile.NewProjectile(source, player.MountedCenter, new Vector2(facing, 0f), ModContent.ProjectileType<FleshMaidenSmash>(),
                    (int)(damage * SmashDamage), knockback * SmashKnockback, player.whoAmI, facing, duration);

                return false;
            }

            Projectile.NewProjectile(source, player.MountedCenter, velocity.SafeNormalize(new Vector2(player.direction, 0f)),
                ModContent.ProjectileType<FleshMaidenStrike>(), damage, knockback, player.whoAmI, state.NextStep(), duration);

            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<Visus>()
                .AddIngredient<EssenceOfTheFouled>(Essence)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }

    /// <summary>
    /// The Flesh Maiden's state on its owner: which step of the combo comes next, how long it
    /// has been since a strike, how long until the smash can come down again, and the throw
    /// back when a smash lands (<see cref="Throw"/>). Only the owner's copy decides anything
    /// (Shoot and CanUseItem run there, and the throw moves only the local player); nothing is
    /// saved.
    /// </summary>
    public class FleshMaidenState : ModPlayer
    {
        /// <summary>The step the next left click plays: 0 the swing down, 1 the swing up, 2 the thrust.</summary>
        public int Step;

        /// <summary>Ticks since a strike or a smash was last out (they reset it every tick they are).</summary>
        public int Idle;

        /// <summary>Ticks until the smash can be struck again, set as one ends (FleshMaidenSmash.OnKill).</summary>
        public int Rest;

        /// <summary>A second without striking and the combo starts over.</summary>
        private const int Forget = 60;

        /// <summary>The last ticks of the rest, over which the eye is half open.</summary>
        private const int EyeOpens = 10;

        public override void PostUpdate()
        {
            if (Rest > 0)
                Rest--;

            if (Idle < Forget)
                Idle++;
            else
                Step = 0;

            if (bleed > 0)
            {
                bleed--;

                if (!Main.dedServ)
                    Spill();
            }
        }

        /// <summary>The step to play now; the one after it comes next.</summary>
        public int NextStep()
        {
            int step = Step;

            Step = (Step + 1) % 3;
            Idle = 0;

            return step;
        }

        /// <summary>The eye when nothing is holding it open: shut while the smash rests, open once it is ready.</summary>
        public int RestingEye => Rest > EyeOpens ? VisusGrip.Shut : Rest > 0 ? VisusGrip.Half : VisusGrip.Open;

        // ---- the throw back ------------------------------------------------------------------------

        /// <summary>How high a smash throws its wielder, and how far back, in pixels: 2 tiles, and 10.</summary>
        public const float ThrowRise = 32f;
        public const float ThrowReach = 160f;

        /// <summary>Ticks the wielder bleeds for, thrown: about as long as the flight.</summary>
        private const int BleedTicks = 26;

        /// <summary>The longest a throw may run, in ticks, whatever happens.</summary>
        private const int ThrowLimit = 90;

        /// <summary>Ticks into the throw, -1 when not thrown. Only the owner's copy counts it.</summary>
        private int thrown = -1;
        private int throwDir;
        private float throwSpeed;
        private float travelled;
        private float lastX;

        private int bleed;
        private int bleedDir;

        /// <summary>
        /// A smash has landed: its wielder is thrown back, away from <paramref name="facing"/>, to
        /// top out 2 tiles up and come down 10 tiles back, bleeding all the way. Called on every
        /// machine the smash runs on, so every one sees the blood; only the owner's copy moves
        /// the player, whose position then goes out the usual way. Not while something else has
        /// hold of them (<see cref="Held"/>).
        ///
        /// Up is one push: the speed that tops out ThrowRise above where it started, counted a
        /// tick at a time as Player.Update counts it (gravity first, then the move), so
        /// v = (g + sqrt(g^2 + 8 g h)) / 2 with the gravity the player has this tick. Back is
        /// steered every tick (PreUpdateMovement, after vanilla's own run and friction, before the
        /// move) by how far the player has really gone, so nothing flings it: 10 tiles, where on
        /// flat ground it comes down, and there it stops dead. Down short of that (a low roof,
        /// rising ground) it skids the rest of the way; a wall stops it. The keys to walk and
        /// jump are held off while it lasts.
        /// </summary>
        public void Throw(int facing)
        {
            bleed = BleedTicks;
            bleedDir = -facing;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.6f, Volume = 0.8f }, Player.Center);

                for (int i = 0; i < 14; i++)
                {
                    Vector2 velocity = new Vector2(bleedDir * Main.rand.NextFloat(0.5f, 3.5f), -Main.rand.NextFloat(1f, 5f) * Player.gravDir);

                    PADParticleHandler.SpawnParticle(new BloodDroplet(Player.Center + Main.rand.NextVector2Circular(8f, 12f), velocity,
                        Main.rand.Next(30, 50), Main.rand.NextFloat(0.6f, 1.05f), Abomanation.Blood));
                }
            }

            if (Player.whoAmI != Main.myPlayer || Held)
                return;

            // Featherfall leaves its gravity signed by gravDir in the field; the pull is the size of it.
            float g = Math.Max(0.01f, Math.Abs(Player.gravity));
            float up = (g + (float)Math.Sqrt(g * g + 8f * g * ThrowRise)) * 0.5f;

            // Ticks until it is back down where it started, counted the same way.
            float airtime = Math.Max(1f, 2f * up / g - 1f);

            thrown = 0;
            throwDir = -facing;
            throwSpeed = ThrowReach / airtime;
            travelled = 0f;
            lastX = Player.position.X;

            Player.velocity.Y = -up * Player.gravDir;
            Player.velocity.X = throwDir * throwSpeed;
            Player.jump = 0;
        }

        public override void SetControls()
        {
            if (thrown < 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
        }

        /// <summary>Something else has hold of the player: dead, frozen, webbed or stoned, on a hook, a rope, a mount, or the Wall's tongue.</summary>
        private bool Held => Player.dead || Player.CCed || Player.tongued || Player.pulley || Player.mount.Active || Player.grappling[0] >= 0;

        public override void PreUpdateMovement()
        {
            if (thrown < 0)
                return;

            // How far it really went last tick: walls, water and slopes all had their say.
            float moved = (Player.position.X - lastX) * throwDir;
            travelled += moved;
            thrown++;

            bool arrived = travelled >= ThrowReach - 0.5f;
            bool blocked = thrown > 1 && moved < 0.25f;

            if (arrived || blocked || thrown > ThrowLimit || Held)
            {
                if (arrived || blocked)
                    Player.velocity.X = 0f;

                thrown = -1;
                return;
            }

            // On course: where it should be by the end of this tick, never past 10 tiles.
            float target = Math.Min(ThrowReach, throwSpeed * thrown);
            Player.velocity.X = throwDir * MathHelper.Clamp(target - travelled, 0f, throwSpeed * 1.5f);
            lastX = Player.position.X;
        }

        /// <summary>Blood off the wielder, thrown: out of the body, falling behind them as they go.</summary>
        private void Spill()
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 from = Player.Center + Main.rand.NextVector2Circular(7f, 12f);
                Vector2 velocity = Player.velocity * 0.25f + new Vector2(-bleedDir * Main.rand.NextFloat(0f, 1.5f),
                    -Main.rand.NextFloat(0.5f, 2.5f) * Player.gravDir) + Main.rand.NextVector2Circular(0.6f, 0.6f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(from, velocity, Main.rand.Next(26, 44),
                    Main.rand.NextFloat(0.5f, 0.95f), Abomanation.Blood));
            }
        }
    }
}
