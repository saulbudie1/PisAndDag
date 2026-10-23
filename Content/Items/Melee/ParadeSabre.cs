using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// What the two Parade Sabres share: the swing's arc (ParadeSabreArc, gold or steel) and the
    /// light off a hit. The sabre is a plain swung sword; the arc is drawn behind it as it goes.
    /// </summary>
    public abstract class ParadeSabreBase : ModItem
    {
        /// <summary>Gold leaf on it still: what the arc and the hits look like.</summary>
        public abstract bool Gilded { get; }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        protected void Defaults(int damage, int useTicks, float knockback)
        {
            Item.width = 48;
            Item.height = 48;

            Item.damage = damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = knockback;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = useTicks;
            Item.useAnimation = useTicks;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;
        }

        /// <summary>Every swing, on the swinger's own machine: the arc that follows it (sent like any projectile).</summary>
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero,
                    ModContent.ProjectileType<ParadeSabreArc>(), 0, 0f, player.whoAmI, Gilded ? 0f : 1f);
            }

            return null;
        }

        /// <summary>A hit that counts against the gold: an enemy, not a critter or anything that cannot be hurt.</summary>
        protected static bool Counts(NPC target) =>
            target.active && !target.friendly && !target.dontTakeDamage && target.lifeMax > 5 && !NPCID.Sets.CountsAsCritter[target.type];
    }

    /// <summary>
    /// The Parade Sabre: white and gold, made for the parade. 20 damage. In the first armoury chest
    /// of Grace, and one in four after it (GraceLoot; Grand worlds), by request (PADWorkFlow §16.98).
    ///
    /// Every hit on an enemy takes a little of its gold leaf off (<see cref="Gilt"/>, counted down
    /// from <see cref="GiltFull"/>, kept with the item and sent with it) and the leaf comes away in
    /// flakes (GiltFlake). When the last of it is gone, at the end of that swing, it is the Parade
    /// Sabre Bare: plain steel, and a better sword for good (<see cref="ParadeSabreBare"/>). Its
    /// prefix goes with it.
    ///
    /// Lore: §XIX, "Grace's claim was favour and what beat it was a better trap": steel under the
    /// blessing. Its armouries are, as the Ward's is built (GraceHighWard), "beautiful and worse
    /// than what is outside the walls".
    /// </summary>
    public class ParadeSabre : ParadeSabreBase
    {
        public const int Damage = 20;
        public const int UseTicks = 22;
        public const float Knockback = 4.5f;

        /// <summary>Hits on enemies before the gold is all gone.</summary>
        public const int GiltFull = 120;

        /// <summary>Flakes off a hit, at the least and at the most; and off the whole blade when the last goes.</summary>
        private const int FlakesLeast = 3, FlakesMost = 5, LastFlakes = 34;

        public int Gilt = GiltFull;

        public override bool Gilded => true;

        public override void SetDefaults()
        {
            Defaults(Damage, UseTicks, Knockback);
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!Counts(target) || Gilt <= 0)
                return;

            Gilt--;

            if (Main.dedServ)
                return;

            Vector2 at = Vector2.Clamp(player.itemLocation + (target.Center - player.itemLocation) * 0.6f, target.TopLeft, target.BottomRight);
            int flakes = Main.rand.Next(FlakesLeast, FlakesMost + 1);

            for (int i = 0; i < flakes; i++)
            {
                Vector2 velocity = new Vector2(player.direction * Main.rand.NextFloat(0.5f, 2.5f), Main.rand.NextFloat(-2.6f, -0.6f));
                PADParticleHandler.SpawnParticle(new GiltFlake(at + Main.rand.NextVector2Circular(6f, 6f), velocity, Main.rand.Next(80, 140)));
            }

            if (Main.rand.NextBool(3))
                PADParticleHandler.SpawnParticle(new CrownGlint(at, 14f, 14, GiltFlake.Bright));

            SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.8f, Volume = 0.22f }, at);
        }

        public override void HoldItem(Player player) => Shed(player);

        public override void UpdateInventory(Player player) => Shed(player);

        /// <summary>
        /// The last of the gold gone: once the swing is over, on the holder's own machine, it is the
        /// bare sabre (its prefix kept; the game sends the slot). A shower of leaf off the blade, and a
        /// bell, once.
        /// </summary>
        private void Shed(Player player)
        {
            if (Gilt > 0 || player.whoAmI != Main.myPlayer || player.itemAnimation > 0)
                return;

            int prefix = Item.prefix;
            Vector2 at = player.Center;

            Item.ChangeItemType(ModContent.ItemType<ParadeSabreBare>());

            if (prefix > 0)
                Item.Prefix(prefix);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.45f, Volume = 0.55f }, at);
            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.8f, Volume = 0.25f }, at);

            for (int i = 0; i < LastFlakes; i++)
            {
                Vector2 from = at + new Vector2(player.direction * Main.rand.NextFloat(6f, 34f), Main.rand.NextFloat(-30f, 4f));
                PADParticleHandler.SpawnParticle(new GiltFlake(from, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-2f, 0.2f)),
                    Main.rand.Next(100, 180)));
            }

            for (int i = 0; i < 3; i++)
            {
                PADParticleHandler.SpawnParticle(new CrownGlint(at + new Vector2(player.direction * Main.rand.NextFloat(10f, 30f), Main.rand.NextFloat(-26f, -4f)),
                    18f, 16 + i * 4, GiltFlake.Bright));
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string text = Language.GetTextValue("Mods.PisAndDag.Items.ParadeSabre.GiltLeft", Gilt);
            int at = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == "Tooltip2");

            TooltipLine left = new TooltipLine(Mod, "GiltLeft", text) { OverrideColor = new Color(232, 196, 112) };

            if (at >= 0)
                tooltips.Insert(at, left);
            else
                tooltips.Add(left);
        }

        public override void SaveData(TagCompound tag)
        {
            if (Gilt < GiltFull)
                tag["Gilt"] = Gilt;
        }

        public override void LoadData(TagCompound tag)
        {
            Gilt = tag.ContainsKey("Gilt") ? Math.Clamp(tag.GetInt("Gilt"), 0, GiltFull) : GiltFull;
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((short)Gilt);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Gilt = reader.ReadInt16();
        }
    }

    /// <summary>
    /// The Parade Sabre with its gold gone: plain steel. 28 damage, a little quicker. Only ever had by
    /// wearing the gilt one down (ParadeSabre).
    /// </summary>
    public class ParadeSabreBare : ParadeSabreBase
    {
        public const int Damage = 28;
        public const int UseTicks = 20;
        public const float Knockback = 5f;

        private static readonly Color[] Steel = { new Color(240, 246, 255), new Color(196, 210, 228), new Color(150, 162, 186) };

        public override bool Gilded => false;

        public override void SetDefaults()
        {
            Defaults(Damage, UseTicks, Knockback);
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 60);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ || !Counts(target))
                return;

            Vector2 at = Vector2.Clamp(player.itemLocation + (target.Center - player.itemLocation) * 0.6f, target.TopLeft, target.BottomRight);
            SparkShard.Spray(at, Steel[0], 3, 3.5f, 16f, 9, falls: false, palette: Steel);
        }
    }
}
