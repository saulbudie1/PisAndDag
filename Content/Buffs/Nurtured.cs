using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What Forest, Nurture Me puts on: the forest keeping the caster. Life and mana come back,
    /// a little armour, a little speed, for as long as it lasts.
    ///
    /// The numbers are here and nowhere else. Life regen is vanilla's lifeRegen, which counts
    /// half points a second and is added straight, past vanilla's own standing-still and
    /// moving rules. Mana goes through ManaRegenPlayer, the mod's mana regen, so it builds back
    /// after a cast like any other. Speed is added in NurturedPlayer.PostUpdateRunSpeeds, after
    /// vanilla has scaled the run speeds, so it is the same five miles an hour on the
    /// stopwatch with or without boots; not on a mount, which runs at its own speeds.
    ///
    /// Everything seen is NurturedPlayer's: the shield and the motes about it.
    /// </summary>
    public class Nurtured : ModBuff
    {
        public const int LifePerSecond = 6;
        public const float ManaPerSecond = 6f;
        public const int Defense = 3;
        public const float Mph = 5f;

        /// <summary>
        /// Pixels a tick in one mile an hour. The stopwatch (Main.DrawInfoAccs) reads a speed
        /// in pixels a tick as speed times 216000 over 42240.
        /// </summary>
        public const float PixelsPerMph = 42240f / 216000f;

        public override void SetStaticDefaults()
        {
            // The forest's keeping does not outlast the world it was asked in.
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.lifeRegen += LifePerSecond * 2;
            ManaRegenPlayer.Add(player, ManaPerSecond);
            player.statDefense += Defense;
            player.GetModPlayer<NurturedPlayer>().Active = true;
        }
    }

    /// <summary>
    /// Nurtured's speed, and everything about it that is seen.
    ///
    /// The shield and its motes are particles, so they are this client's alone, and every
    /// client makes its own for every player it sees holding the buff: buffs are sent to other
    /// players, particles are not. Only the owner knows how long is left (other clients are
    /// told only that a buff is there), so only the owner's shield fades out over the last
    /// second; on other screens it goes when the buff does.
    /// </summary>
    public class NurturedPlayer : ModPlayer
    {
        /// <summary>Set by the buff every tick it is on; cleared every tick.</summary>
        public bool Active;

        private NurtureShield shield;
        private int lastTime;

        public override void ResetEffects() => Active = false;

        public override void PostUpdateRunSpeeds()
        {
            if (!Active || Player.mount.Active)
                return;

            float faster = Nurtured.Mph * Nurtured.PixelsPerMph;
            Player.maxRunSpeed += faster;
            Player.accRunSpeed += faster;
        }

        public override void PostUpdate()
        {
            if (Main.dedServ)
                return;

            int type = ModContent.BuffType<Nurtured>();
            int index = Player.FindBuffIndex(type);
            int time = index >= 0 ? Player.buffTime[index] : 0;

            if (index >= 0 && !Player.dead && (shield == null || shield.Gone))
            {
                shield = new NurtureShield(Player.whoAmI);
                PADParticleHandler.SpawnParticle(shield);

                for (int i = 0; i < NurtureShield.Motes; i++)
                    PADParticleHandler.SpawnParticle(new NurtureMote(shield, i));

                lastTime = time;
                return;
            }

            // Asked again before it ran out: the time jumps back up. The shield answers with a
            // pulse. Only the owner can see the time.
            if (Player.whoAmI == Main.myPlayer && shield != null && !shield.Gone && time > lastTime + 30)
                shield.Refresh();

            lastTime = time;
        }
    }
}
