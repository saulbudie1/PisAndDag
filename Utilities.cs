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
    }
}
