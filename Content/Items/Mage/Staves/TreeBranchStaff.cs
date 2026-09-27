using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Staves
{
    /// <summary>
    /// A branch off a tree, and the plainest staff there is. Casts whichever spell is up, and
    /// nothing else.
    ///
    /// Sold by the Dryad (<see cref="TreeBranchStaffSource"/>). Placeholder art: see
    /// PlaceholderTextures.md.
    /// </summary>
    public class TreeBranchStaff : Catalyst
    {
        public override CastKind Kind => CastKind.Spell;

        public override void SetStaticDefaults()
        {
            // The sprite lies on the diagonal, head up and right, like every vanilla staff;
            // this is what tells vanilla to turn it a further 45 degrees when it points it.
            Item.staff[Type] = true;
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 40;
            Item.height = 40;
        }
    }

    /// <summary>Where the staff comes from: the Dryad's shop, always.</summary>
    public class TreeBranchStaffSource : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Dryad)
                shop.Add(ModContent.ItemType<TreeBranchStaff>());
        }
    }
}
