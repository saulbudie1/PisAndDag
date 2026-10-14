using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Dryad's Blessing, a four leaf clover, by request: "Will survive an attack that will kill you
    /// with 25 HP, Gives you a buff that boosts your regen by 20hp/s for 5 sec and 1.5 sec of
    /// invinciblity ... On surviving: plays a good sound effect you create and have some effect of
    /// leaves circling. Cooldown for this: 15 mins or respawn. Obtained after engaging in dryad's
    /// questline: Questline I update later." Then: "Blessing cool down to 10 mins".
    ///
    /// Worn, a hit that would kill its wearer leaves them standing at 25 life instead (all their life,
    /// if they have less than that at most), and a second and a half in which no hit can land (the
    /// game's own time after a hit, for every kind of hit at once). Then Spared, 5 seconds of 20 more
    /// life a second, with the clover's sound and leaves going round them; and Wilted Clover, 10
    /// minutes before it can spare them again, or until they next die. What it keeps them from is in
    /// <see cref="DryadsBlessingPlayer"/>.
    ///
    /// The numbers are as asked. Against others like it they are mild: Calamity's, for comparison,
    /// give back far more life on far shorter waits (its Nebulous Core 90 seconds, its Permafrost's
    /// Concoction three minutes), though later in a game; the game itself has nothing that keeps a
    /// player from death.
    ///
    /// Where it comes from: the Dryad, by request: "1/4 chance the dryad will talk something about
    /// Bezoar is needed to heal her wound. If player has bezoar in inventory, then there will be a
    /// button "The Bezoar" with its icon. clicking it will have the dryad thanking you and giving you
    /// the blessing." She takes the Bezoar, once for each player (DryadBezoarTalk). Mine: Orange
    /// rarity; its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class DryadsBlessing : ModItem
    {
        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<DryadsBlessingPlayer>().Worn = true;
        }

        // Recipes stay commented out for now; none was asked for. It comes from the Dryad, for a Bezoar.
    }

    /// <summary>
    /// What the clover keeps its wearer from: a hit that would kill them. Any hit through the game's
    /// Player.Hurt, whatever dealt it, the world's own included (a fall, lava, spikes); not a death
    /// that is not a hit (burning, poison and the other harm a second, drowning, and the deaths the
    /// mod's own story deals out by name).
    ///
    /// How: the game takes a hit's damage off a player's life (Player.Hurt, after OnHurt) and, at 0,
    /// calls KillMe straight after, on that player's own machine. OnHurt says whether this hit is the
    /// one; PreKill, on the wearer's own machine, keeps them alive (<see cref="Spare"/>) if it is, the
    /// clover is worn, and it is not wilted. Every other machine is told their life (the game's own
    /// message) and sees the buffs come on; each shows the clover's sound, its burst and its leaves
    /// the tick Spared is first on.
    /// </summary>
    public class DryadsBlessingPlayer : ModPlayer
    {
        /// <summary>The life it leaves its wearer with.</summary>
        public const int SparedLife = 25;

        /// <summary>A second and a half in which no hit can land.</summary>
        public const int Invincible = 90;

        public static readonly SoundStyle Sound = new SoundStyle("PisAndDag/Content/Sounds/DryadsBlessing")
        {
            Volume = 0.85f,
        };

        /// <summary>Worn this tick (DryadsBlessing.UpdateAccessory).</summary>
        public bool Worn;

        /// <summary>Worn when everything worn last had its say (PostUpdateEquips); held across the tick.</summary>
        private bool wearing;

        /// <summary>The hit being taken now will take the last of their life (OnHurt); read and cleared by PreKill.</summary>
        private bool lethal;

        /// <summary>Spared was on last tick: the bloom is shown the tick it comes on.</summary>
        private bool wasSpared;

        public override void ResetEffects()
        {
            Worn = false;
        }

        public override void PostUpdateEquips()
        {
            wearing = Worn;
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            lethal = Player.statLife - info.Damage <= 0;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            bool fromHit = lethal;
            lethal = false;

            if (!fromHit || !wearing || Player.whoAmI != Main.myPlayer || Player.HasBuff(ModContent.BuffType<WiltedClover>()))
                return true;

            Spare();
            return false;
        }

        /// <summary>Kept alive: 25 life, a second and a half untouchable, Spared, and the clover wilts.</summary>
        private void Spare()
        {
            Player.statLife = Math.Min(SparedLife, Player.statLifeMax2);
            Player.HealEffect(Player.statLife);
            Player.SetImmuneTimeForAllTypes(Invincible);
            Player.AddBuff(ModContent.BuffType<Spared>(), Spared.Duration);
            Player.AddBuff(ModContent.BuffType<WiltedClover>(), WiltedClover.Duration);

            // The hit has already told everyone their life was gone.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
        }

        public override void UpdateLifeRegen()
        {
            if (Player.HasBuff(ModContent.BuffType<Spared>()))
                Player.lifeRegen += Spared.LifeRegen;
        }

        public override void PostUpdate()
        {
            lethal = false;

            int index = Player.FindBuffIndex(ModContent.BuffType<Spared>());
            bool spared = index >= 0;

            if (spared && !wasSpared)
                Bloom();

            wasSpared = spared;

            // A green light about them while the leaves are out, gone with them.
            if (spared && !Main.dedServ)
            {
                float strength = DryadsBlessingLeaves.Fade(Spared.Duration - Player.buffTime[index]);
                Lighting.AddLight(Player.Center, 0.30f * strength, 0.55f * strength, 0.22f * strength);
            }
        }

        /// <summary>The moment it spares them: the clover's sound, and a burst of green motes and grass.</summary>
        private void Bloom()
        {
            if (Main.dedServ)
                return;

            Vector2 centre = Player.Center;
            SoundEngine.PlaySound(Sound, centre);

            for (int i = 0; i < 28; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(4f, 3f) * Main.rand.NextFloat(0.5f, 1f);
                Dust mote = Dust.NewDustPerfect(centre, DustID.DryadsWard, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                mote.noGravity = true;
            }

            for (int i = 0; i < 14; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-3.5f, -1f));
                Dust.NewDustPerfect(centre + Main.rand.NextVector2Circular(10f, 16f), DustID.GrassBlades, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.2f));
            }

            for (int i = 0; i < 8; i++)
            {
                Dust spark = Dust.NewDustPerfect(centre + Main.rand.NextVector2Circular(16f, 22f), DustID.GreenFairy, Main.rand.NextVector2Circular(1f, 1f), 0, default, Main.rand.NextFloat(0.8f, 1.1f));
                spark.noGravity = true;
            }
        }
    }

    /// <summary>
    /// The leaves that go round a player while they are Spared: ten on a tilted ring about their
    /// middle, six of the game's own forest leaves turning over as they go (the falling leaf of a
    /// tree, Gore 910, unpainted) and four of the clover's own (DryadsBlessingLeaf.png), turning.
    /// The near half of the ring is drawn over the player and the far half behind them, darker and
    /// smaller. They burst out from the player in the first third of a second, breathe in and out a
    /// little as they go, and in the last three quarters of a second drift outward and up and fade.
    /// Everything follows from how long the buff has been on, so every machine draws the same.
    /// </summary>
    public static class DryadsBlessingLeaves
    {
        /// <summary>The ring's width and how flat it is seen: its depth is this share of its width.</summary>
        private const float Radius = 32f;
        private const float Flat = 0.3f;

        /// <summary>Turns a tick, in radians: a turn in about a second and a half.</summary>
        private const float Turn = 0.07f;

        private const int Leaves = 10;

        /// <summary>Which places round the ring are the clover's own leaves: four, spread between the six others.</summary>
        private static readonly bool[] CloverAt = { true, false, false, true, false, true, false, false, true, false };

        /// <summary>How long the burst out and the drift away take, in ticks.</summary>
        private const int Burst = 18;
        private const int Drift = 45;

        /// <summary>The game's forest leaf: one of 32 columns (by paint) and 8 frames, each 10 by 12 in a 12 by 14 cell.</summary>
        private const int LeafGore = GoreID.TreeLeaf_Normal;
        private const int LeafColumns = 32;
        private const int LeafFrames = 8;

        private static Asset<Texture2D> clover;

        public static void Load()
        {
            if (!Main.dedServ)
                clover = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/DryadsBlessingLeaf");
        }

        public static void Unload()
        {
            clover = null;
        }

        /// <summary>How much of the leaves' light is left, from 0 to 1, this many ticks after Spared came on.</summary>
        public static float Fade(int ticks)
        {
            float alpha = Math.Clamp(ticks / 8f, 0f, 1f);
            int left = Spared.Duration - ticks;

            if (left < Drift)
                alpha *= Math.Max(0f, left / (float)Drift);

            return alpha;
        }

        public static bool Showing(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && !drawInfo.drawPlayer.dead
            && drawInfo.drawPlayer.HasBuff(ModContent.BuffType<Spared>());

        public static void Draw(ref PlayerDrawSet drawInfo, bool near)
        {
            Player player = drawInfo.drawPlayer;
            int index = player.FindBuffIndex(ModContent.BuffType<Spared>());

            if (index < 0 || clover == null)
                return;

            int ticks = Math.Clamp(Spared.Duration - player.buffTime[index], 0, Spared.Duration);
            float fade = Fade(ticks);

            if (fade <= 0f)
                return;

            Main.instance.LoadGore(LeafGore);
            Texture2D leaf = TextureAssets.Gore[LeafGore].Value;
            Texture2D leaflet = clover.Value;

            int cellWidth = leaf.Width / LeafColumns;
            int cellHeight = leaf.Height / LeafFrames;

            Vector2 centre = new Vector2(
                (int)(drawInfo.Position.X - Main.screenPosition.X + player.width / 2f),
                (int)(drawInfo.Position.Y - Main.screenPosition.Y + player.height / 2f));

            Color light = Lighting.GetColor((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
            Color lit = Color.Lerp(light, Color.White, 0.55f);

            float size = Spread(ticks);
            float rise = 0f;
            int left = Spared.Duration - ticks;

            if (left < Drift)
                rise = (1f - left / (float)Drift) * 18f;

            for (int i = 0; i < Leaves; i++)
            {
                float angle = MathHelper.TwoPi * i / Leaves + Turn * ticks + (i % 2) * 0.35f;
                float depth = (float)Math.Sin(angle);

                if (depth >= 0f != near)
                    continue;

                float radius = Radius * size * (1f + (i % 3 - 1) * 0.12f);
                float lift = (i % 4 - 1.5f) * 4.5f;

                Vector2 at = centre + new Vector2((float)Math.Cos(angle) * radius, depth * radius * Flat + lift - rise);
                float nearness = (depth + 1f) / 2f;
                float scale = MathHelper.Lerp(0.8f, 1.1f, nearness);
                Color colour = Color.Lerp(lit.MultiplyRGB(new Color(150, 165, 150)), lit, nearness) * fade;

                if (CloverAt[i])
                {
                    float spin = angle * 2f + i;
                    drawInfo.DrawDataCache.Add(new DrawData(leaflet, at, null, colour, spin, leaflet.Size() / 2f, scale, SpriteEffects.None, 0f));
                }
                else
                {
                    int frame = (ticks / 5 + i * 3) % LeafFrames;
                    Rectangle source = new Rectangle(0, frame * cellHeight, cellWidth - 2, cellHeight - 2);
                    float tilt = angle + MathHelper.PiOver2;
                    drawInfo.DrawDataCache.Add(new DrawData(leaf, at, source, colour, tilt, source.Size() / 2f, scale, SpriteEffects.None, 0f));
                }
            }
        }

        /// <summary>How far out the ring is, as a share of its full width: out with a little overshoot, breathing, then away.</summary>
        private static float Spread(int ticks)
        {
            int left = Spared.Duration - ticks;

            if (ticks < Burst)
            {
                // Out past where it settles, and back (an ease out with overshoot).
                float t = ticks / (float)Burst;
                const float Back = 1.7f;
                float u = t - 1f;
                return 0.2f + 0.8f * (1f + (Back + 1f) * u * u * u + Back * u * u);
            }

            float breath = 1f + 0.04f * (float)Math.Sin(ticks * 0.1f);

            if (left < Drift)
                breath += (1f - left / (float)Drift) * 0.8f;

            return breath;
        }
    }

    /// <summary>The far half of the leaves, behind the player and their wings.</summary>
    public class DryadsBlessingLeavesBack : PlayerDrawLayer
    {
        public override void Load() => DryadsBlessingLeaves.Load();

        public override void Unload() => DryadsBlessingLeaves.Unload();

        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.Wings);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => DryadsBlessingLeaves.Showing(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo) => DryadsBlessingLeaves.Draw(ref drawInfo, false);
    }

    /// <summary>The near half of the leaves, over everything of the player's, the Bone Casing's ribs included.</summary>
    public class DryadsBlessingLeavesFront : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => DryadsBlessingLeaves.Showing(drawInfo);

        protected override void Draw(ref PlayerDrawSet drawInfo) => DryadsBlessingLeaves.Draw(ref drawInfo, true);
    }
}
