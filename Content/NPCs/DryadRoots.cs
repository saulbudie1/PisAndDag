using PisAndDag.Content.Items.Mage.Spell;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Dryad's second quest, after the Bezoar (DryadBezoar), by request: Deep Roots "Dropped by Finishing Dryad's
    /// Quest", "A new quest you design after bezoar quest". Mine, all of it.
    ///
    /// Once a player has brought her the Bezoar, one chat in <see cref="HintChance"/> is the roots
    /// (<see cref="DryadRootsHint"/>, Mods.PisAndDag.Hints.DryadRoots): healed, she can hear the roots again, and some
    /// have not stopped growing; things with mouths come up on them. She wants them cut back and <see cref="Needed"/>
    /// of the roots brought. While she is asking it of them, the plants with mouths (the Man Eater, the Snatcher, the
    /// Angry Trapper) drop an Overgrown Root for them one time in <see cref="DropChance"/>
    /// (<see cref="RootsAsked"/>). With <see cref="Needed"/> in their pack, "The roots" is offered under her dialogue;
    /// clicking it takes them, and she thanks them and gives them Deep Roots, the spell
    /// (Mods.PisAndDag.Hints.DryadRootsThanks). Once for each player, kept with the player
    /// (<see cref="DryadRootsPlayer"/>), as the Bezoar is; after that the hint, the line and the drops are gone for them.
    ///
    /// The words (Lore Bible §VI, Life and Mors: nothing prunes, and the Dryad may be Life's vessel): that some roots
    /// do not stop is said; why they do not is not. The hint points at the plants by what they are, not at a place.
    /// </summary>
    public class DryadRoots : ModSystem
    {
        /// <summary>Roots she asks for.</summary>
        public const int Needed = 6;

        /// <summary>One chat in this many is the hint, while she is asking.</summary>
        public const int HintChance = 4;

        /// <summary>One plant in this many drops a root for a player she is asking.</summary>
        public const int DropChance = 2;

        /// <summary>Whether she is asking it of this player now: the Bezoar brought, the roots not yet.</summary>
        public static bool Asking(Player player) =>
            player.GetModPlayer<DryadBezoarPlayer>().Healed && !player.GetModPlayer<DryadRootsPlayer>().Done;

        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Dryad, new TalkOption(
                TalkOptions.Icon(ModContent.ItemType<OvergrownRoot>()) + "The roots",
                npc => Give(),
                (player, npc) => Asking(player) && player.CountItem(ModContent.ItemType<OvergrownRoot>(), Needed) >= Needed));
        }

        /// <summary>On the clicking player's own machine: the roots from them, the spell to them.</summary>
        private static void Give()
        {
            Player me = Main.LocalPlayer;
            int root = ModContent.ItemType<OvergrownRoot>();

            if (!Asking(me) || me.CountItem(root, Needed) < Needed)
                return;

            for (int i = 0; i < Needed; i++)
                me.ConsumeItem(root);

            me.GetModPlayer<DryadRootsPlayer>().Done = true;
            me.QuickSpawnItem(me.GetSource_GiftOrReward("PisAndDag:DryadRoots"), ModContent.ItemType<DeepRoots>(), 1);
            TalkOptions.Say(Language.GetTextValue("Mods.PisAndDag.Hints.DryadRootsThanks"));
        }
    }

    /// <summary>The roots, one chat in <see cref="DryadRoots.HintChance"/>, while she is asking it of the player talking.</summary>
    public class DryadRootsHint : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Dryad;

        public override void GetChat(NPC npc, ref string chat)
        {
            if (!DryadRoots.Asking(Main.LocalPlayer))
                return;

            if (Main.rand.NextBool(DryadRoots.HintChance))
                chat = Language.GetTextValue("Mods.PisAndDag.Hints.DryadRoots");
        }
    }

    /// <summary>The roots, from the plants with mouths, for a player she is asking.</summary>
    public class DryadRootsDrops : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type is NPCID.ManEater or NPCID.Snatcher or NPCID.AngryTrapper;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.ByCondition(new RootsAsked(), ModContent.ItemType<OvergrownRoot>(), DryadRoots.DropChance));
        }
    }

    /// <summary>The player the kill is for is one the Dryad is asking for roots.</summary>
    public class RootsAsked : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => info.player != null && DryadRoots.Asking(info.player);

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => "While the Dryad wants the roots";
    }

    /// <summary>Whether this player has brought the Dryad her roots. Saved with the player.</summary>
    public class DryadRootsPlayer : ModPlayer
    {
        public bool Done;

        public override void SaveData(TagCompound tag)
        {
            if (Done)
                tag["DryadRootsDone"] = true;
        }

        public override void LoadData(TagCompound tag)
        {
            Done = tag.GetBool("DryadRootsDone");
        }
    }
}
