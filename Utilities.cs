using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using PisAndDag.Content.PADplayer;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameContent.Prefixes;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ObjectData;
using Terraria.UI.Chat;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using PisAndDag;
using PisAndDag.Content.Items;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.Projectiles;
namespace PisAndDag
{
    public static class Utilities
    {

        public static PisPlayer PIS(this Player player)
        {
            return player.GetModPlayer<PisPlayer>();
        }
        public static PSItem PIS(this Item item)
        {
            return item.GetGlobalItem<PSItem>();
        }

        // ---- PAD(): one accessor per entity type -----------------------------
        //
        // The point of the family is that mod state on ANY entity is reached the same
        // way, so a piece of gear writes `player.PAD().SurgingCore = true;` from
        // UpdateAccessory without knowing which class holds the flag, and a projectile
        // reads `target.PAD()` without a GetGlobalNPC call spelled out at every site.
        // Calamity does exactly this with .Calamity().
        //
        // Deliberately separate from PIS(): PisPlayer owns the camera, the shake and the
        // flight gate — things the mod does TO the view, which persist across ticks.
        // PADPlayer owns effect flags that equipment sets and ResetEffects clears every
        // tick. Keeping them apart means the per-tick wipe can never touch a live pan.

        public static PADPlayer PAD(this Player player)
        {
            return player.GetModPlayer<PADPlayer>();
        }

        public static PADGlobalNPC PAD(this NPC npc)
        {
            return npc.GetGlobalNPC<PADGlobalNPC>();
        }

        public static PADGlobalProjectile PAD(this Projectile projectile)
        {
            return projectile.GetGlobalProjectile<PADGlobalProjectile>();
        }

        public static PADGlobalItem PAD(this Item item)
        {
            return item.GetGlobalItem<PADGlobalItem>();
        }
    }
}
