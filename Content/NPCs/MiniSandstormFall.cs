using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The fall after Mini Sandstorm's tornado (Projectiles.Magic.MiniSandstormTornado), by request: "it shoots npc
    /// upwards with falling damage". The game hurts no NPC for falling; this does, for one the tornado has carried:
    /// from the highest it got to where it lands, past four tiles, a share of the tornado's damage, more the further
    /// (a quarter of it at four tiles, all of it at ten and a quarter, two and a half times it at most). Struck where
    /// the world is run, and told to everyone; seen everywhere.
    ///
    /// <see cref="Weight"/> is how carriable a thing is, for the tornado too.
    /// </summary>
    public class MiniSandstormFall : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Whether the tornado has carried it, and has not been landed from yet; the damage to go by; the highest its feet got.</summary>
        private bool carried;
        private int damage;
        private float highest;
        private int since;
        private bool falling;

        /// <summary>Past this many tiles a fall hurts. Mine.</summary>
        public const float Harmless = 4f;

        /// <summary>
        /// How carriable <paramref name="npc"/> is, 0 (not at all) to a little over 1: how far the game would knock it
        /// back, less for the bigger (a slime's size is 1). Nothing for a boss, a thing that flies or goes through
        /// walls, or one the game will not knock back.
        /// </summary>
        public static float Weight(NPC npc)
        {
            if (npc.boss || npc.noGravity || npc.noTileCollide || npc.knockBackResist <= 0f)
                return 0f;

            float size = MathHelper.Clamp((float)Math.Sqrt(900f / Math.Max(1f, npc.width * npc.height)), 0.45f, 1.15f);
            float weight = MathHelper.Clamp(npc.knockBackResist, 0f, 1f) * size;

            return weight < 0.1f ? 0f : weight;
        }

        /// <summary>From the tornado, each tick it carries this one.</summary>
        public void Lifted(int owner, int tornadoDamage)
        {
            if (!carried)
                highest = float.MaxValue;

            // Caught again on the way down: the fall starts over from wherever it is carried to.
            falling = false;
            carried = true;
            damage = Math.Max(damage, tornadoDamage);
            since = 0;
        }

        public override void PostAI(NPC npc)
        {
            if (!carried)
                return;

            since++;
            highest = Math.Min(highest, npc.Bottom.Y);

            if (npc.velocity.Y > 1f)
                falling = true;

            // Landed: falling, and stopped by the ground.
            bool landed = falling && npc.velocity.Y == 0f && since > 2;

            if (landed)
            {
                float tiles = (npc.Bottom.Y - highest) / 16f;

                if (tiles > Harmless)
                {
                    float share = MathHelper.Clamp(0.25f + (tiles - Harmless) * 0.12f, 0.25f, 2.5f);
                    int hurt = Math.Max(1, (int)(damage * share));

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        npc.SimpleStrikeNPC(hurt, 0, false, 0f, DamageClass.Magic);

                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = MathHelper.Clamp(0.25f + tiles * 0.03f, 0.25f, 0.7f), Pitch = -0.2f }, npc.Bottom);
                        Harenaeum.Burst(npc.Bottom, -Vector2.UnitY, 10 + (int)tiles, 3.5f, 1.3f, 1f);
                    }
                }

                Reset();
                return;
            }

            // Never landed (into water, onto a platform it went through, carried away for good): let it go.
            if (since > 600)
                Reset();
        }

        private void Reset()
        {
            carried = false;
            falling = false;
            damage = 0;
            since = 0;
        }
    }
}
