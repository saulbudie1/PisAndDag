using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Dryad's wound, and where Dryad's Blessing comes from, by request: "1/4 chance the dryad
    /// will talk something about Bezoar is needed to heal her wound. If player has bezoar in
    /// inventory, then there will be a button "The Bezoar" with its icon. clicking it will have the
    /// dryad thanking you and giving you the blessing."
    ///
    /// Until a player has brought her one, one chat in <see cref="DryadBezoarHint.HintChance"/> is
    /// the wound (<see cref="DryadBezoarHint"/>); while they carry a Bezoar, "The Bezoar" is offered
    /// under her dialogue. Clicking it takes one Bezoar from them, and she thanks them and gives them
    /// the clover. Mine: it takes the Bezoar, and it is once for each player, kept with the player
    /// (<see cref="DryadBezoarPlayer"/>), so in a world of several each can bring her one; after that
    /// the line and the hint are gone for them.
    ///
    /// The words (Lore Bible: the Dryad, and XVII, the Jungle): that there is a wound, and what would
    /// draw it out, are said; what made it, and what is in it, are not. The Bezoar comes from the
    /// hornets and the Toxic Sludge, one in a hundred (a second try in Expert), so the hint points at
    /// the hornets and not at a place.
    /// </summary>
    public class DryadBezoarTalk : ModSystem
    {
        private const string Thanks =
            "Thank you. I can feel it working already. Take this. Clover comes up wherever I rest, " +
            "and this one came up with four leaves. Keep it on you. Once in a while, it will not let " +
            "you fall.";

        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Dryad, new TalkOption(
                TalkOptions.Icon(ItemID.Bezoar) + "The Bezoar",
                npc => Give(),
                (player, npc) => player.HasItem(ItemID.Bezoar) && !player.GetModPlayer<DryadBezoarPlayer>().Healed));
        }

        /// <summary>On the clicking player's own machine: a Bezoar from them, the clover to them.</summary>
        private static void Give()
        {
            Player me = Main.LocalPlayer;
            DryadBezoarPlayer quest = me.GetModPlayer<DryadBezoarPlayer>();

            if (quest.Healed || !me.ConsumeItem(ItemID.Bezoar))
                return;

            quest.Healed = true;
            me.QuickSpawnItem(me.GetSource_GiftOrReward("PisAndDag:DryadBezoar"), ModContent.ItemType<DryadsBlessing>(), 1);
            TalkOptions.Say(Thanks);
        }
    }

    /// <summary>The wound, one chat in <see cref="HintChance"/>, until the player talking has brought her a Bezoar.</summary>
    public class DryadBezoarHint : GlobalNPC
    {
        /// <summary>"1/4 chance".</summary>
        public const int HintChance = 4;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Dryad;

        public override void GetChat(NPC npc, ref string chat)
        {
            if (Main.LocalPlayer.GetModPlayer<DryadBezoarPlayer>().Healed)
                return;

            if (Main.rand.NextBool(HintChance))
                chat = Language.GetTextValue("Mods.PisAndDag.Hints.DryadBezoar");
        }
    }

    /// <summary>Whether this player has brought the Dryad her Bezoar. Saved with the player.</summary>
    public class DryadBezoarPlayer : ModPlayer
    {
        public bool Healed;

        public override void SaveData(TagCompound tag)
        {
            if (Healed)
                tag["DryadBezoarHealed"] = true;
        }

        public override void LoadData(TagCompound tag)
        {
            Healed = tag.GetBool("DryadBezoarHealed");
        }
    }
}
