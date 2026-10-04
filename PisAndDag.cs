using System.IO;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Every packet this mod sends starts with one of these, saying which part of the mod
    /// it is for. <see cref="PisAndDag.HandlePacket"/> reads it and hands the rest on.
    /// </summary>
    public enum PADPacket : byte
    {
        /// <summary>The Kingdom of Grace's guard map. See GraceWorld.</summary>
        Grace,

        /// <summary>Client to server: the Guide has said his line about the orb. See GuideHints.</summary>
        GuideSpokeOfLegends,

        /// <summary>
        /// A player taken by one of the Wall's tentacles: client to server from the one
        /// taken, server to the other clients. See WallGrabPlayer.
        /// </summary>
        WallGrab,

        /// <summary>Server to clients: something behind the Wall was eaten. See LegendsWall.</summary>
        WallAte,

        /// <summary>Client to server: the player she stopped has answered Dona Quinn. See DonaQuinnEncounter.</summary>
        DonaQuinn,

        /// <summary>Client to server: from the player the Guide's first words are for. See GuideIntro.</summary>
        GuideIntro,

        /// <summary>
        /// A parry: client to server from the player who made it (what they turned aside, and
        /// where), server to the other clients (the spark). See Parry. What it does to the
        /// thing goes with the thing; the parry's blow is a hit like any other.
        /// </summary>
        Parry,
    }

    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class PisAndDag : Mod
    {
        public override void Load()
        {
            // Screen filters are a client-side drawing concern; a dedicated server has no
            // graphics device to register them against.
            if (Main.dedServ)
                return;

            LoadWastedMileFilter();
            LoadGraceFilter();
            LoadGraceDeepFilter();
            LoadMorsCloak();
            LoadManaNauseaFilter();
        }

        /// <summary>
        /// Other mods' way in. ("FireProjectile", type), ("FireItem", type) or ("FireDebuff", type):
        /// that counts as fire for Rage! Sweet Fire (Content.Combat.FireAffinity). ("ParryNPC", type),
        /// ("ParryNoNPC", type), ("ParryNoStagger", type), ("ParryProjectile", type) or
        /// ("ParryNoProjectile", type): what the parry makes of that enemy or that shot
        /// (Content.Combat.Parry.Call). Returns true when taken.
        /// </summary>
        public override object Call(params object[] args) => Content.Combat.Parry.Call(args) ?? Content.Combat.FireAffinity.Call(args);

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var kind = (PADPacket)reader.ReadByte();

            switch (kind)
            {
                case PADPacket.Grace:
                    Content.Structures.Grace.GraceWorld.HandlePacket(this, reader, whoAmI);
                    break;

                case PADPacket.GuideSpokeOfLegends:
                    Content.NPCs.GuideHints.Heard();
                    break;

                case PADPacket.WallGrab:
                    Content.NPCs.Legends.WallGrabPlayer.HandlePacket(this, reader, whoAmI);
                    break;

                case PADPacket.WallAte:
                    Content.NPCs.Legends.LegendsWall.HandleAte(reader);
                    break;

                case PADPacket.DonaQuinn:
                    Content.NPCs.DonaQuinn.DonaQuinnEncounter.HandlePacket(reader, whoAmI);
                    break;

                case PADPacket.GuideIntro:
                    Content.NPCs.GuideIntro.HandlePacket(reader, whoAmI);
                    break;

                case PADPacket.Parry:
                    Content.Combat.Parry.HandlePacket(reader, whoAmI);
                    break;

                default:
                    Logger.Warn($"A packet of unknown kind {(byte)kind} came from {whoAmI} and was ignored.");
                    break;
            }
        }

        // RequestAssetIfExists, not Assets.Request. Request records the asset in the
        // repository even when it fails, and tModLoader re-throws that later from
        // Mod.TransferAllAssets() — so a try/catch at the call site does not help.
        // This asks first and registers nothing when the file is absent.
        //
        // tModLoader does NOT compile .fx during a mod build. Every .fx under Effects/
        // is source only: run it through fxcompiler or EasyXnb and put the resulting
        // .xnb beside it. Until that exists, the mod loads without that effect and
        // logs a line saying so.

        /// <summary>
        /// The grade over the Wasted Mile. The biome switches it on and off through
        /// Player.ManageSpecialBiomeVisuals, which needs the key to exist in
        /// Filters.Scene first; WastedMileBiome.FilterReady is what tells it that it does.
        /// </summary>
        private void LoadWastedMileFilter()
        {
            if (!RequestAssetIfExists("Effects/WastedMile", out Asset<Effect> mile))
            {
                Logger.Warn(
                    "Wasted Mile screen filter disabled: Effects/WastedMile.xnb is missing. " +
                    "Compile Effects/WastedMile.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "MilePass" must match the pass name in the .fx technique block. Intensity
            // is the overall strength; the fade in and out is the filter's own opacity.
            Filters.Scene[Content.Biomes.WastedMileBiome.FilterKey] = new Filter(
                new ScreenShaderData(mile, "MilePass").UseIntensity(0.85f),
                EffectPriority.High);

            Filters.Scene[Content.Biomes.WastedMileBiome.FilterKey].Load();

            Content.Biomes.WastedMileBiome.FilterReady = true;
        }

        /// <summary>
        /// The light over the Kingdom of Grace. The biome switches it on and off through
        /// Player.ManageSpecialBiomeVisuals, as the Mile's; GraceLight feeds it the sun every
        /// frame it is in use. GraceBiome.FilterReady is what tells both that the key is there.
        /// </summary>
        private void LoadGraceFilter()
        {
            if (!RequestAssetIfExists("Effects/Grace", out Asset<Effect> grace))
            {
                Logger.Warn(
                    "Kingdom of Grace screen filter disabled: Effects/Grace.xnb is missing. " +
                    "Compile Effects/Grace.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "GracePass" must match the pass name in the .fx technique block.
            Filters.Scene[Content.Biomes.GraceBiome.FilterKey] = new Filter(
                new ScreenShaderData(grace, "GracePass").UseIntensity(1f),
                EffectPriority.High);

            Filters.Scene[Content.Biomes.GraceBiome.FilterKey].Load();

            Content.Biomes.GraceBiome.FilterReady = true;
        }

        /// <summary>
        /// The grade Beneath Grace. The biome switches it on and off through
        /// Player.ManageSpecialBiomeVisuals, as the plain's; its colours are fixed here, the
        /// gold of the light and the blue of the dark. GraceDeepBiome.FilterReady is what tells
        /// the biome the key is there.
        /// </summary>
        private void LoadGraceDeepFilter()
        {
            if (!RequestAssetIfExists("Effects/GraceDeep", out Asset<Effect> deep))
            {
                Logger.Warn(
                    "Beneath Grace screen filter disabled: Effects/GraceDeep.xnb is missing. " +
                    "Compile Effects/GraceDeep.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "GraceDeepPass" must match the pass name in the .fx technique block.
            Filters.Scene[Content.Biomes.GraceDeepBiome.FilterKey] = new Filter(
                new ScreenShaderData(deep, "GraceDeepPass")
                    .UseIntensity(1f)
                    .UseColor(1f, 0.8f, 0.48f)
                    .UseSecondaryColor(0.22f, 0.42f, 0.8f),
                EffectPriority.High);

            Filters.Scene[Content.Biomes.GraceDeepBiome.FilterKey].Load();

            Content.Biomes.GraceDeepBiome.FilterReady = true;
        }

        /// <summary>
        /// The sway on Mors's cloak. A sprite shader, applied by MorsIdle itself at draw
        /// time — nothing to register, it only has to exist. Missing means a still cloak.
        /// </summary>
        private void LoadMorsCloak()
        {
            if (!RequestAssetIfExists("Effects/MorsCloak", out Asset<Effect> cloak))
            {
                Logger.Warn(
                    "Mors cloak sway disabled: Effects/MorsCloak.xnb is missing. " +
                    "Compile Effects/MorsCloak.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            Content.NPCs.Mors.MorsIdle.CloakEffect = cloak;
        }

        /// <summary>
        /// What Mana Nausea does to the screen. ManaNauseaScreen switches it on and off from
        /// the local player's buffs and needs the key in Filters.Scene first; Registered is
        /// what tells it the key is there. Missing means the buffs are only icons.
        /// </summary>
        private void LoadManaNauseaFilter()
        {
            if (!RequestAssetIfExists("Effects/ManaNausea", out Asset<Effect> nausea))
            {
                Logger.Warn(
                    "Mana Nausea screen filter disabled: Effects/ManaNausea.xnb is missing. " +
                    "Compile Effects/ManaNausea.fx with fxcompiler or EasyXnb and place " +
                    "the .xnb beside it.");
                return;
            }

            // "NauseaPass" must match the pass name in the .fx technique block. VeryHigh: the
            // game's config can raise a threshold (FilterPriorityThreshold) under which
            // filters are not drawn, and this one is a buff's effect, not scenery.
            Filters.Scene[Content.Buffs.ManaNauseaScreen.FilterKey] = new Filter(
                new ScreenShaderData(nausea, "NauseaPass"),
                EffectPriority.VeryHigh);

            Filters.Scene[Content.Buffs.ManaNauseaScreen.FilterKey].Load();

            Content.Buffs.ManaNauseaScreen.Registered = true;
        }
    }
}
