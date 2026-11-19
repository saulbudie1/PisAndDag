using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Accessories.Tentochi;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Projectiles.Magic;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Armor
{
    /// <summary>
    /// Sandclad, the armour made of Harenaeum (by request: "Create an armorset from the ores that is from the desert.
    /// Tailored for Mage and Summoner"): a chestplate, greaves, and two heads, the mage's headscarf and the summoner's
    /// sun hat, each with its own set bonus.
    ///
    ///   Bustshield   8 defence, +9% critical strike chance
    ///   Greaves      5 defence, +4 mph (SandcladPlayer: 0.78 pixels a tick on what the player can run at)
    ///   Headscarf    3 defence, +5% magic damage. Set: standing still raises a sandstorm about the player that hurts
    ///                what is in it and gives mana for it (SandcladStorm); the key throws the player to the cursor on
    ///                a gust of sand, 50 tiles at most, every 30 seconds.
    ///   Sun Hat      3 defence, +5% summon damage. Set: two sandstorms in the air circle the player (SandcladDevil);
    ///                the key gathers them at the cursor, raging, for a while.
    ///
    /// Worn, they are the game's own pictures for now (PlaceholderTextures.md): the Dye Trader's turban, the summer hat,
    /// the fossil plate and greaves. Their pictures in the inventory are drawn in code. Mine: every number not asked for.
    /// </summary>
    public static class Sandclad
    {
        public const int Bars = 0;

        /// <summary>The mage's storm: how long stood still before it rises, how far it reaches, its damage, how often, the mana a hit and a kill give.</summary>
        public const int StillTicks = 45;
        public const float StormReach = 9f * 16f;
        public const int StormDamage = 16, StormEvery = 30, ManaHit = 4, ManaKill = 20;

        /// <summary>The mage's gust (by request: 50 tiles, 30 seconds).</summary>
        public const float GustFar = 50f * 16f;
        public const int GustTicks = 22, GustCooldown = 30 * 60;

        /// <summary>The summoner's storms: their damage, how often each hurts; raging: how long, times its damage and size, how often, the cooldown.</summary>
        public const int DevilDamage = 14, DevilEvery = 20;
        public const int RageTicks = 6 * 60, RageEvery = 8, RageCooldown = 20 * 60;
        public const float RageDamage = 1.5f, RageSize = 1.6f;

        public static string Key => TentochiItem.KeyName(PADKeybinds.ArmorSet);

        /// <summary>
        /// A ribbon of blown sand: a smooth line along <paramref name="path"/> (0 its tail to 1 its head), thin and faint
        /// at its tail, thicker and brighter toward its head, a pale thread down its middle. Seen only.
        /// </summary>
        public static void Ribbon(Microsoft.Xna.Framework.Graphics.SpriteBatch batch, Func<float, Vector2> path, float width, float alpha, int pieces = 14)
        {
            if (alpha <= 0.01f)
                return;

            Vector2 last = path(0f);

            for (int i = 1; i <= pieces; i++)
            {
                float f = i / (float)pieces;
                Vector2 next = path(f);
                float k = alpha * f * (f > 0.9f ? (1f - f) / 0.1f * 0.6f + 0.4f : 1f);
                global::PisAndDag.Content.NPCs.Mech.DestroyerArt.DrawLine(batch, last, next, width * (0.35f + 0.65f * f), Harenaeum.Sand[1] * (0.55f * k));
                global::PisAndDag.Content.NPCs.Mech.DestroyerArt.DrawLine(batch, last, next, Math.Max(1f, width * 0.3f * f), Harenaeum.Sand[3] * (0.8f * k));
                last = next;
            }
        }

        public static bool Set(Item head, Item body, Item legs, int headType) =>
            head.type == headType && body.type == ModContent.ItemType<SandcladBustshield>() && legs.type == ModContent.ItemType<SandcladGreaves>();

        public static void Recipe(ModItem item, int bars)
        {
            item.CreateRecipe().AddIngredient(ModContent.ItemType<HarenaeumBar>(), bars).AddTile(TileID.Anvils).Register();
        }
    }

    public class SandcladBustshield : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 22;
            Item.defense = 8;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
            Item.bodySlot = ArmorIDs.Body.FossilPlate;
        }

        public override void UpdateEquip(Player player) => player.GetCritChance(DamageClass.Generic) += 9f;

        public override void AddRecipes() => Sandclad.Recipe(this, 20);
    }

    public class SandcladGreaves : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 18;
            Item.defense = 5;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1);
            Item.legSlot = ArmorIDs.Legs.FossilGreaves;
        }

        public override void UpdateEquip(Player player) => player.GetModPlayer<SandcladPlayer>().Greaves = true;

        public override void AddRecipes() => Sandclad.Recipe(this, 15);
    }

    public class SandcladHeadscarf : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 22;
            Item.defense = 3;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(silver: 80);
            Item.headSlot = ArmorIDs.Head.DyeTradersTurban;
        }

        public override void UpdateEquip(Player player) => player.GetDamage(DamageClass.Magic) += 0.05f;

        public override bool IsArmorSet(Item head, Item body, Item legs) => Sandclad.Set(head, body, legs, Type);

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = Language.GetTextValue("Mods.PisAndDag.Sandclad.MageSet", Sandclad.Key);
            player.GetModPlayer<SandcladPlayer>().MageSet = true;
        }

        public override void AddRecipes() => Sandclad.Recipe(this, 10);
    }

    public class SandcladSunHat : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 18;
            Item.defense = 3;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(silver: 80);
            Item.headSlot = ArmorIDs.Head.SummerHat;
        }

        public override void UpdateEquip(Player player) => player.GetDamage(DamageClass.Summon) += 0.05f;

        public override bool IsArmorSet(Item head, Item body, Item legs) => Sandclad.Set(head, body, legs, Type);

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = Language.GetTextValue("Mods.PisAndDag.Sandclad.SummonSet", Sandclad.Key);
            player.GetModPlayer<SandcladPlayer>().SummonSet = true;
        }

        public override void AddRecipes() => Sandclad.Recipe(this, 10);
    }

    /// <summary>What wearing Sandclad does past its numbers: the greaves' speed, the two sets' storms and their key.</summary>
    public class SandcladPlayer : ModPlayer
    {
        public bool Greaves, MageSet, SummonSet;

        /// <summary>Ticks stood still (the mage's storm rises at Sandclad.StillTicks).</summary>
        public int Still;

        /// <summary>The summoner's storms raging: ticks left, and where to (the owner's cursor; the storms' places are what is told to others).</summary>
        public int Rage;

        /// <summary>The gust: ticks left, and its speed.</summary>
        private int gust;
        private Vector2 gustSpeed;

        public override void ResetEffects()
        {
            Greaves = MageSet = SummonSet = false;
        }

        public override void UpdateDead()
        {
            Still = Rage = gust = 0;
        }

        /// <summary>4 mph: the game shows 216000 / 42240 mph for a pixel a tick.</summary>
        public override void PostUpdateRunSpeeds()
        {
            if (!Greaves)
                return;

            const float more = 4f * 42240f / 216000f;
            Player.maxRunSpeed += more;
            Player.accRunSpeed += more;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (PADKeybinds.ArmorSet == null || !PADKeybinds.ArmorSet.JustPressed || Player.dead || Player.CCed)
                return;

            int cooldown = ModContent.BuffType<SandcladSpent>();

            if (Player.HasBuff(cooldown))
                return;

            if (MageSet)
            {
                Vector2 to = Main.MouseWorld - Player.Center;
                float far = Math.Min(to.Length(), Sandclad.GustFar);

                if (far < 32f)
                    return;

                gust = Sandclad.GustTicks;
                Projectile.NewProjectile(Player.GetSource_Misc("Sandclad"), Player.Center, Vector2.Zero, ModContent.ProjectileType<SandcladGust>(), 0, 0f, Player.whoAmI);
                gustSpeed = to.SafeNormalize(Vector2.UnitX) * far / Sandclad.GustTicks;
                Player.AddBuff(cooldown, Sandclad.GustCooldown);

                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.8f, Pitch = -0.2f }, Player.Center);
                Harenaeum.Burst(Player.Center, -gustSpeed, 24, 9f, 0.9f, 2f);
            }
            else if (SummonSet)
            {
                Rage = Sandclad.RageTicks;
                Player.AddBuff(cooldown, Sandclad.RageCooldown);
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.8f, Pitch = 0.2f }, Player.Center);
            }
        }

        public override void PreUpdateMovement()
        {
            if (gust <= 0)
                return;

            gust--;
            Player.velocity = gust == 0 ? gustSpeed * 0.25f : gustSpeed;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.armorEffectDrawShadow = true;

            if (!Main.dedServ && gust == 0)
                Harenaeum.Burst(Player.Bottom, gustSpeed, 20, 8f, 1.2f, 2f);
        }

        public override void PostUpdate()
        {
            Still = Player.velocity.LengthSquared() < 0.04f && gust <= 0 ? Still + 1 : 0;

            if (Rage > 0)
                Rage--;

            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            if (MageSet && Still == Sandclad.StillTicks && Player.ownedProjectileCounts[ModContent.ProjectileType<SandcladStorm>()] == 0)
                Projectile.NewProjectile(Player.GetSource_Misc("Sandclad"), Player.Center, Vector2.Zero, ModContent.ProjectileType<SandcladStorm>(), Sandclad.StormDamage, 0f, Player.whoAmI);

            if (!SummonSet)
                return;

            int devil = ModContent.ProjectileType<SandcladDevil>();
            bool first = false, second = false;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner != Player.whoAmI || p.type != devil)
                    continue;

                if ((int)p.ai[0] == 0)
                    first = true;
                else
                    second = true;
            }

            if (!first)
                Projectile.NewProjectile(Player.GetSource_Misc("Sandclad"), Player.Center, Vector2.Zero, devil, Sandclad.DevilDamage, 0f, Player.whoAmI, 0f);

            if (!second)
                Projectile.NewProjectile(Player.GetSource_Misc("Sandclad"), Player.Center, Vector2.Zero, devil, Sandclad.DevilDamage, 0f, Player.whoAmI, 1f);
        }
    }
}
