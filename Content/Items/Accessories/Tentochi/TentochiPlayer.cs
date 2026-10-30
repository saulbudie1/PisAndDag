using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Projectiles.Tentochi;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// What the Made By Tentochi accessories do, worn (each sets its flag from UpdateAccessory and the rest is here):
    ///
    /// <list type="bullet">
    /// <item>Enterprise's Complimentary: a laser from the wearer's middle toward the cursor while a ranged weapon is held
    /// (drawn by TentochiLayers.Sight, the wearer's own machine only).</item>
    /// <item>Exo Air I and II: 7 and 12 mph more top speed running (the game's stopwatch: a pixel a tick is 216000 / 42240
    /// mph, Player's own sum), not on a mount and not added to wings' own speed; their dashes are AccessoryDash's. The
    /// better worn is the one that counts.</item>
    /// <item>Back Rocket: every 2 seconds, while an enemy is in sight within 50 tiles, a rocket off the back that sways and
    /// homes in on it (TentochiRocket), 80 damage. Nothing while none is: the clock waits at its end, so one comes the moment
    /// one is seen.</item>
    /// <item>Calamity Module: adrenaline filling 100 in 30 seconds; full, its key (PADKeybinds.Adrenaline) lets it go for 5
    /// seconds of 2.5 times the damage of everything of the wearer's that hits (AdrenalineRush).</item>
    /// <item>Adam Kit I: 15 defense (the item) and a tenth off every speed (TopSpeed.Slow, as the HEATR slows: the top speed
    /// running and mounted, and the jump's speed, which is also what caps every climb: wings', Favor of an Empress's,
    /// rocket boots', a flying mount's); its key (PADKeybinds.Detonate) lets it go: 25,000 damage to every enemy within 16
    /// tiles, 450 to the wearer whatever they wear, and Potion Sickness after (by request: "cannot heal is potion
    /// sickness"), for as long as a healing potion gives it.</item>
    /// </list>
    ///
    /// The keys are read and the rockets fired only on the wearer's own machine; the others are told of a detonation or an
    /// adrenaline let go (PADPacket.Tentochi) and see it. The Everclear Mirror's copy wears what its owner wears: it runs as
    /// fast and is as slowed, but fires no rockets and lets nothing go.
    /// </summary>
    public class TentochiPlayer : ModPlayer
    {
        // ---- the numbers, by request ------------------------------------------------------------------------------------

        /// <summary>Miles an hour in a pixel a tick, as the game's stopwatch reckons them (216000 / 42240).</summary>
        public const float MphPerPixel = 216000f / 42240f;

        public const float ExoAirIMph = 7f;
        public const float ExoAirIIMph = 12f;

        public const int RocketInterval = 120;
        public const int RocketDamage = 80;
        public const float RocketKnockback = 4f;

        /// <summary>How far the launcher looks: 50 tiles.</summary>
        public const float RocketRange = 800f;

        public const float AdrenalineMax = 100f;

        /// <summary>A tick's worth: 100 in 30 seconds.</summary>
        public const float AdrenalinePerTick = AdrenalineMax / (30f * 60f);

        public const int AdrenalineTicks = 5 * 60;
        public const float AdrenalineDamage = 2.5f;

        /// <summary>What is left of a hit taken with the meter full.</summary>
        public const float AdrenalineGuard = 0.5f;

        public const int KitDefense = 15;
        public const float KitSpeed = 0.9f;
        public const int KitSelfDamage = 450;
        public const int KitDamage = 25000;

        /// <summary>The blast's edge: 16 tiles.</summary>
        public const float KitRadius = 16f * 16f;

        /// <summary>After a detonation, the ticks before the key does anything again, so one press is one blast.</summary>
        public const int KitRearm = 60;

        // ---- worn this tick -------------------------------------------------------------------------------------------

        public bool Sight;
        public int Exo;
        public bool ExoShown;
        public Item Launcher;
        public bool LauncherShown;
        public bool Module;
        public bool Kit;
        public bool KitShown;

        /// <summary>Adrenaline let go and running (AdrenalineRush, set by the buff each tick).</summary>
        public bool Surging;

        // ---- kept between ticks ---------------------------------------------------------------------------------------

        public float Adrenaline;

        /// <summary>Ticks till the launcher can fire again; it waits at 0 for something to fire at.</summary>
        public int RocketClock;

        /// <summary>The NPC the launcher holds, or -1.</summary>
        public int Target = -1;

        /// <summary>Ticks it has held it, for the lock's look.</summary>
        public int Held;

        /// <summary>Ticks since a rocket left the launcher, for its kick.</summary>
        public int SinceShot = 999;

        /// <summary>Ticks adrenaline has run, for its look.</summary>
        public int SurgeTime;

        private int rearm;
        private bool chimed;

        /// <summary>Ticks till the kit can be let go again: its core beats fast and unsteady meanwhile (TentochiKitCore).</summary>
        public int Rearm => rearm;

        public bool LocalAndReal => Player.whoAmI == Main.myPlayer && !MimicSystem.Is(Player);

        public override void ResetEffects()
        {
            Sight = false;
            Exo = 0;
            ExoShown = false;
            Launcher = null;
            LauncherShown = false;
            Module = false;
            Kit = false;
            KitShown = false;
            Surging = false;
        }

        public override void UpdateDead()
        {
            Adrenaline = 0f;
            chimed = false;
            Target = -1;
            Held = 0;
        }

        // ---- speed ----------------------------------------------------------------------------------------------------

        /// <summary>The top speed running on boots, as the game had it before wings, in the air, take the run's speeds over (Player.WingAirLogicTweaks).</summary>
        private float groundAcc;

        public override void PostUpdateMiscEffects()
        {
            groundAcc = Player.accRunSpeed;
        }

        /// <summary>
        /// After the game and everything worn have set the run's speeds: the exoskeleton's top speed added to the run (by
        /// request: "increase run speed"), not to the wings'. In the air on wings the game gives the wings' own speed where
        /// it is the faster (Player.WingAirLogicTweaks); there, the run with the exoskeleton counts where it is the faster
        /// instead, as boots do. Adam Kit's tenth comes off after this, with everything else (TopSpeed).
        /// </summary>
        public override void PostUpdateRunSpeeds()
        {
            if (Exo <= 0 || Player.mount.Active)
                return;

            float more = (Exo >= 2 ? ExoAirIIMph : ExoAirIMph) / MphPerPixel;
            Player.maxRunSpeed += more;

            if (Player.wingsLogic > 0 && Player.velocity.Y != 0f && !Player.merman)
                Player.accRunSpeed = Math.Max(Player.accRunSpeed, groundAcc + more);
            else
                Player.accRunSpeed += more;
        }

        // ---- every tick -----------------------------------------------------------------------------------------------

        public override void PostUpdate()
        {
            if (SinceShot < 999)
                SinceShot++;

            if (Surging)
            {
                SurgeTime++;

                if (!Main.dedServ)
                    TentochiFX.SurgeTick(Player, SurgeTime);
            }
            else
                SurgeTime = 0;

            if (rearm > 0)
                rearm--;

            if (!LocalAndReal)
            {
                // Seen from elsewhere, the launcher holds what its last rocket was sent at (Fired), and lets go of it when
                // none has come for a while.
                if (Target >= 0)
                    Held++;

                if (SinceShot > RocketInterval + 30)
                {
                    Target = -1;
                    Held = 0;
                }

                return;
            }

            if (Player.dead)
                return;

            Fill();
            Watch();
        }

        /// <summary>
        /// A rocket of theirs seen leaving the launcher (TentochiRocket, its first tick, on every machine): on the others'
        /// machines, where nothing else tells it, the launcher's kick and what it holds. The wearer's own knows already.
        /// </summary>
        public void Fired(int target)
        {
            if (LocalAndReal)
                return;

            SinceShot = 0;

            if (target != Target)
            {
                Target = target;
                Held = 0;
            }
        }

        /// <summary>The module's adrenaline, filling while it is worn and not running; a sound once, as it fills.</summary>
        private void Fill()
        {
            if (!Module)
            {
                Adrenaline = 0f;
                chimed = false;
                return;
            }

            if (Surging)
                return;

            Adrenaline = Math.Min(AdrenalineMax, Adrenaline + AdrenalinePerTick);

            if (Adrenaline >= AdrenalineMax && !chimed)
            {
                chimed = true;
                SoundEngine.PlaySound(TentochiSounds.Ready);
            }
        }

        /// <summary>The launcher: what it holds, and a rocket at it when its clock has run down.</summary>
        private void Watch()
        {
            if (Launcher == null)
            {
                Target = -1;
                Held = 0;
                return;
            }

            if (RocketClock > 0)
                RocketClock--;

            int found = Find();

            if (found != Target)
            {
                Target = found;
                Held = 0;
            }
            else if (found >= 0)
                Held++;

            if (found < 0 || RocketClock > 0)
                return;

            RocketClock = RocketInterval;
            SinceShot = 0;

            Vector2 from = LauncherAt(Player);
            Vector2 velocity = Vector2.Normalize(new Vector2(-Player.direction * 0.45f, -1f * Player.gravDir)) * 7f;
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(RocketDamage);

            Projectile.NewProjectile(Player.GetSource_Accessory(Launcher), from, velocity, ModContent.ProjectileType<TentochiRocket>(),
                damage, RocketKnockback, Player.whoAmI, found);
        }

        /// <summary>
        /// Where rockets leave the launcher on the back: the mouth of its left tube (TentochiLauncher.Seat and Tubes, from the
        /// body frame's middle, which is 3 pixels over the player's).
        /// </summary>
        public static Vector2 LauncherAt(Player player) => player.MountedCenter + new Vector2(-player.direction * 17f, -9f * player.gravDir);

        /// <summary>
        /// The enemy the launcher should hold: the one the wearer's minions are told to attack, if it is in sight and in
        /// range; else the nearest in sight and in range. -1 for none.
        /// </summary>
        private int Find()
        {
            Vector2 from = LauncherAt(Player);

            if (Player.HasMinionAttackTargetNPC)
            {
                NPC chosen = Main.npc[Player.MinionAttackTargetNPC];

                if (Fits(chosen, from))
                    return chosen.whoAmI;
            }

            int best = -1;
            float nearest = RocketRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.CanBeChasedBy())
                    continue;

                float distance = Vector2.Distance(from, npc.Center);

                if (distance < nearest && Collision.CanHitLine(from, 1, 1, npc.position, npc.width, npc.height))
                {
                    best = i;
                    nearest = distance;
                }
            }

            return best;
        }

        private static bool Fits(NPC npc, Vector2 from) => npc.CanBeChasedBy() && Vector2.Distance(from, npc.Center) < RocketRange
            && Collision.CanHitLine(from, 1, 1, npc.position, npc.width, npc.height);

        // ---- the keys -------------------------------------------------------------------------------------------------

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!LocalAndReal || Player.dead)
                return;

            if (PADKeybinds.Adrenaline != null && PADKeybinds.Adrenaline.JustPressed && Module && Adrenaline >= AdrenalineMax && !Surging)
                LetGo();

            if (PADKeybinds.Detonate != null && PADKeybinds.Detonate.JustPressed && Kit && rearm == 0)
                Detonate();
        }

        /// <summary>Adrenaline let go: five seconds of it (AdrenalineRush), the meter empty, and the others told.</summary>
        private void LetGo()
        {
            Adrenaline = 0f;
            chimed = false;
            Player.AddBuff(ModContent.BuffType<AdrenalineRush>(), AdrenalineTicks);
            TentochiFX.Surge(Player);
            TentochiHUD.Flash(0.35f);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                Send(Signal.Surge, Player.Center);
        }

        /// <summary>
        /// Adam Kit I let go. Every enemy within 16 tiles (its hitbox's nearest point within reach of the wearer's middle)
        /// takes 25,000, exactly, whatever its defense: a thing of many parts that share one life (a worm, a boss's hands)
        /// once, not once a part. Then the wearer takes 450, exactly, whatever they wear, and cannot dodge it (spared only in
        /// Journey's godmode, as the game's own hurts are); and if they live, Potion Sickness, for as long as a healing
        /// potion would give it them (Player.ApplyPotionDelay: a minute, the Philosopher's Stone's share off).
        /// </summary>
        private void Detonate()
        {
            rearm = KitRearm;
            Vector2 at = Player.Center;
            HashSet<int> struck = new HashSet<int>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage || npc.CountsAsACritter || npc.life <= 0)
                    continue;

                if (npc.immortal && npc.type != NPCID.TargetDummy)
                    continue;

                if (!Player.CanNPCBeHitByPlayerOrPlayerProjectile(npc))
                    continue;

                Vector2 nearest = Vector2.Clamp(at, npc.TopLeft, npc.BottomRight);

                if (Vector2.Distance(nearest, at) > KitRadius)
                    continue;

                int life = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;

                if (!struck.Add(life))
                    continue;

                NPC.HitInfo hit = new NPC.HitInfo
                {
                    Damage = KitDamage,
                    SourceDamage = KitDamage,
                    Knockback = 10f,
                    HitDirection = npc.Center.X < at.X ? -1 : 1,
                    Crit = false,
                    DamageType = DamageClass.Default,
                };

                npc.StrikeNPC(hit);

                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendStrikeNPC(npc, hit);
            }

            TentochiFX.Detonation(at, KitRadius);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                Send(Signal.Detonation, at);

            Player.HurtInfo hurt = new Player.HurtInfo
            {
                SourceDamage = KitSelfDamage,
                Damage = KitSelfDamage,
                DamageSource = PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.Decommissioned", Player.name)),
                Dodgeable = false,
                HitDirection = 0,
                Knockback = 0f,
            };

            // This overload of Hurt does not ask about godmode; the game's others do, and spare the player.
            if (!Player.creativeGodMode)
                Player.Hurt(hurt);

            if (!Player.dead)
            {
                // As a healing potion gives it (Player.ApplyPotionDelay): its time, with the Philosopher's Stone's share off.
                int delay = Math.Max(60, (int)Player.PotionDelayModifier.ApplyTo(Player.potionDelayTime));
                Player.potionDelay = delay;
                Player.AddBuff(BuffID.PotionSickness, delay);
            }
        }

        // ---- adrenaline's damage --------------------------------------------------------------------------------------

        /// <summary>
        /// Every hit of theirs (PlayerLoader: the item's and the projectile's hits come through here first, and so do the
        /// ones made by the player's body: thorns, a mount's stomp, a minecart, a shield's bash, a dash), 2.5 times.
        /// </summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Surging)
                modifiers.FinalDamage *= AdrenalineDamage;
        }

        /// <summary>A hit taken with the meter full does half its damage (by request: "full adrenaline and gets hit half damage taken").</summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (Module && !Surging && Adrenaline >= AdrenalineMax)
                modifiers.FinalDamage *= AdrenalineGuard;
        }

        /// <summary>
        /// Any hurt empties the meter (by request: "any damage reset adrenaline"): every hit, the kit's own detonation
        /// among them. What the game takes away a little at a time (poison, burning, drowning) is no hit and leaves it.
        /// </summary>
        public override void OnHurt(Player.HurtInfo info)
        {
            Adrenaline = 0f;
            chimed = false;
        }

        // ---- the look -----------------------------------------------------------------------------------------------

        private static int drawingSplit;

        /// <summary>Adrenaline running: the wearer split in red and cyan, a pixel or three either side, pulsing with it.</summary>
        public override void DrawPlayer(Camera camera)
        {
            if (!Surging || Player.invis || Player.dead || Player.stoned || drawingSplit != 0)
                return;

            AccessoryDash dash = Player.GetModPlayer<AccessoryDash>();

            if (dash.Dashing || dash.Since < 10)
                return;

            float pulse = 2f + 1.5f * (float)Math.Sin(SurgeTime * 0.35f);
            Vector2 at = Player.position + new Vector2(0f, Player.gfxOffY);

            try
            {
                drawingSplit = 1;
                Main.PlayerRenderer.DrawPlayer(camera, Player, at - new Vector2(pulse, 0f), Player.fullRotation, Player.fullRotationOrigin, 0.55f);
                drawingSplit = 2;
                Main.PlayerRenderer.DrawPlayer(camera, Player, at + new Vector2(pulse, 0f), Player.fullRotation, Player.fullRotationOrigin, 0.6f);
            }
            finally
            {
                drawingSplit = 0;
            }
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (drawingSplit != 0)
            {
                Color tint = drawingSplit == 1 ? TentochiFX.Red : TentochiFX.Cyan;
                r = tint.R / 255f;
                g = tint.G / 255f;
                b = tint.B / 255f;
                fullBright = true;
                return;
            }

            if (Surging && drawInfo.shadow == 0f)
            {
                // Flushed with it: their own colours pushed toward the red, a little lit from within.
                float heat = 0.3f + 0.1f * (float)Math.Sin(SurgeTime * 0.35f);
                r = MathHelper.Lerp(r, 1f, heat);
                g *= 1f - heat * 0.5f;
                b *= 1f - heat * 0.4f;
            }
        }

        // ---- the others' machines ------------------------------------------------------------------------------------

        public enum Signal : byte { Detonation, Surge }

        private void Send(Signal signal, Vector2 at, int to = -1, int except = -1)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)PADPacket.Tentochi);
            packet.Write((byte)signal);
            packet.Write((byte)Player.whoAmI);
            packet.WriteVector2(at);
            packet.Send(to, except);
        }

        internal static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            Signal signal = (Signal)reader.ReadByte();
            int who = reader.ReadByte();
            Vector2 at = reader.ReadVector2();

            if (Main.netMode == NetmodeID.Server)
                who = whoAmI;

            if (who < 0 || who >= Main.maxPlayers || !Main.player[who].active)
                return;

            Player player = Main.player[who];

            if (Main.netMode == NetmodeID.Server)
            {
                player.GetModPlayer<TentochiPlayer>().Send(signal, at, -1, whoAmI);
                return;
            }

            if (signal == Signal.Detonation)
            {
                TentochiFX.Detonation(at, KitRadius);

                // Its core beating unsteady after, as it does on the wearer's own machine (TentochiKitCore).
                player.GetModPlayer<TentochiPlayer>().rearm = KitRearm;
            }
            else if (signal == Signal.Surge)
                TentochiFX.Surge(player);
        }
    }
}
