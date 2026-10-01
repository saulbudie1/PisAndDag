using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What Rage! Sweet Fire puts on: while it lasts, the player's fire hits a quarter harder.
    /// What counts as fire is FireAffinity's: flamethrowers, the sun's weapons, and anything
    /// that sets what it hits alight. The number is here and nowhere else.
    ///
    /// Everything seen is SweetFirePlayer's: the ring of fire going round the player.
    /// </summary>
    public class SweetFire : ModBuff
    {
        /// <summary>Added to a fire hit's damage, as vanilla adds "increased damage".</summary>
        public const float Bonus = 0.25f;

        public override void SetStaticDefaults()
        {
            // The fire's rage does not outlast the world it was asked in.
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SweetFirePlayer>().Active = true;
        }
    }

    /// <summary>
    /// Sweet Fire's damage, and the ring that shows it.
    ///
    /// The damage is worked out where the hit is, on the machine of the player whose hit it is,
    /// which is the machine that knows its own buffs. The ring is a particle, so each client
    /// makes its own for every player it sees holding the buff: buffs are sent to other players,
    /// particles are not.
    /// </summary>
    public class SweetFirePlayer : ModPlayer
    {
        /// <summary>Set by the buff every tick it is on; cleared every tick.</summary>
        public bool Active;

        private SweetFireAura aura;

        public override void ResetEffects() => Active = false;

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Active && FireAffinity.Is(proj, Player))
                modifiers.SourceDamage += SweetFire.Bonus;
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Active && FireAffinity.Is(item, Player))
                modifiers.SourceDamage += SweetFire.Bonus;
        }

        public override void PostUpdate()
        {
            if (Main.dedServ || Player.dead || !Player.HasBuff<SweetFire>())
                return;

            if (aura == null || aura.Gone)
            {
                aura = new SweetFireAura(Player.whoAmI);
                PADParticleHandler.SpawnParticle(aura);
            }
        }
    }
}
