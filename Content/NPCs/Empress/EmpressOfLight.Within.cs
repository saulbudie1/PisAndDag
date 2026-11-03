using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- she fights within her arena; the day held outside it burns ------------------------------------------------
    //
    // By request: "Make empress of light only try to fight inside her decagon", "can go outside for a bit but will go back
    // inside while fighting". Through vanilla's own moves (which go by where the one she fights is: over them, past them in
    // a dash) she may cross out of her arena; after a second outside she makes her way back in, faster the longer she has
    // been out, until she is a little way in from its border. Not in a scene or an act of hers (they place her
    // themselves), not while she leaves at the day's end (vanilla's state 13), not with nobody left to fight.
    //
    // And: "Anything outside her decagon daylight suffer 30k dps a second (property like her dash and hold and light up in
    // phase 2)". While her second half holds the day (Legends), whatever is outside her arena and within its reach (as far
    // as her sky goes: EmpressArena.Reach) burns as her hand burns whoever she holds: thirty thousand life a second, as fire
    // takes it (no healing of its own meanwhile, through any defence, nothing to dodge), lit white with her light while it
    // does. A player, on their own machine; a creature, on every machine as the game counts a burn. Not a townsperson, nor
    // what cannot be hurt, nor herself.

    public partial class EmpressOfLight
    {
        /// <summary>Ticks she may be outside her arena before she makes her way back in; how far in from its border she goes.</summary>
        private const int OutsideGrace = 60;
        private const float KeepInMargin = 96f;

        /// <summary>Her way back in: this fast a tick at first, this much faster each tick after, and no faster than this.</summary>
        private const float ReturnPace = 6f;
        private const float ReturnGains = 0.3f;
        private const float ReturnMost = 26f;

        /// <summary>The server's: ticks she has been outside her arena; whether she is on her way back in.</summary>
        private int outsideFor;
        private bool returning;

        /// <summary>
        /// Every tick, after vanilla's move (PostAI), the server's: outside her arena too long, she makes her way back in,
        /// whatever vanilla had her doing (what of her speed took her further out taken off). Her place is sent with her.
        /// </summary>
        private void KeepIn(NPC npc)
        {
            if (!Authority || !arenaSet || scene != Scene.None || dying || npc.ai[0] == 13f || TargetOf(npc) == null)
            {
                outsideFor = 0;
                returning = false;
                return;
            }

            if (Decagon.Inside(npc.Center, arenaCentre, EmpressArena.Radius, returning ? KeepInMargin : 0f))
            {
                outsideFor = 0;
                returning = false;
                return;
            }

            outsideFor++;

            if (!returning && outsideFor <= OutsideGrace)
                return;

            returning = true;

            Vector2 velocity = npc.velocity;
            Vector2 inside = Decagon.PushInside(npc.Center, arenaCentre, EmpressArena.Radius, KeepInMargin, ref velocity);
            Vector2 toward = inside - npc.Center;
            float distance = toward.Length();

            if (distance > 0.5f)
            {
                float pace = Math.Min(ReturnMost, ReturnPace + ReturnGains * Math.Max(0, outsideFor - OutsideGrace));
                npc.position += toward / distance * Math.Min(pace, distance);
            }

            npc.velocity = velocity;

            if (outsideFor % 10 == 0)
                npc.netUpdate = true;
        }

        // ---- the day held outside ---------------------------------------------------------------------------------

        /// <summary>Her arena while her second half holds the day, as she last said (every tick she holds it): its middle, its radius, until when, and whose.</summary>
        private static Vector2 dayCentre;
        private static float dayRadius;
        private static uint dayUntil;
        private static int dayBy = -1;

        /// <summary>Every tick, every machine (PostAI): while her second half holds the day, where her arena is.</summary>
        private void MarkDay(NPC npc)
        {
            if (!arenaSet || !unveiled || !LegendsSecond(npc))
                return;

            dayCentre = arenaCentre;
            dayRadius = EmpressArena.Radius;
            dayUntil = Main.GameUpdateCount + 2;
            dayBy = npc.whoAmI;
        }

        /// <summary>Whether this is out in the day she holds: outside her arena and within its reach, while her second half holds the day.</summary>
        internal static bool InHeldDay(Rectangle box)
        {
            if (dayBy < 0 || Main.GameUpdateCount > dayUntil || dayUntil - Main.GameUpdateCount > 2)
                return false;

            NPC her = Main.npc[dayBy];

            if (!her.active || her.type != NPCID.HallowBoss)
                return false;

            Vector2 at = box.Center.ToVector2();
            return !Decagon.Inside(at, dayCentre, dayRadius) && Vector2.Distance(at, dayCentre) <= EmpressArena.Reach;
        }

        /// <summary>The day's light on whatever burns in it, seen: a glint on it now and then, and its light.</summary>
        internal static void DayLight(Vector2 at, Vector2 size)
        {
            if (Main.dedServ)
                return;

            Lighting.AddLight(at, 1.1f, 1f, 1.05f);

            if (Main.GameUpdateCount % 4 == 0)
            {
                Vector2 on = at + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * size.X, Main.rand.NextFloat(-0.5f, 0.5f) * size.Y);
                PADParticleHandler.SpawnParticle(new BloomGeneric(on, new Vector2(0f, -1.2f), Main.rand.NextBool() ? Color.White : EmpressArt.Pink, 0.35f, 16));
            }
        }
    }

    /// <summary>A creature out in the day the Empress holds burns (EmpressOfLight.InHeldDay): thirty thousand life a second, lit white.</summary>
    public class EmpressDayCreature : GlobalNPC
    {
        /// <summary>Life a second the day takes; and how much of it each number shown is.</summary>
        public const int LifeASecond = 30000;
        private const int Shown = 3000;

        private static bool Burns(NPC npc) =>
            npc.active && npc.type != NPCID.HallowBoss && !npc.townNPC && !npc.dontTakeDamage && !npc.immortal && EmpressOfLight.InHeldDay(npc.Hitbox);

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!Burns(npc))
                return;

            // As fire takes it: none of its own healing, and the loss counted by the game (twice it, a second).
            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= LifeASecond * 2;

            if (damage < Shown)
                damage = Shown;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (!Burns(npc))
                return;

            drawColor = Color.Lerp(drawColor, Color.White, 0.7f);
            EmpressOfLight.DayLight(npc.Center, npc.Size);
        }
    }

    /// <summary>A player out in the day the Empress holds burns, on their own machine (EmpressOfLight.InHeldDay): as her hand burns, lit white.</summary>
    public class EmpressDayPlayer : ModPlayer
    {
        /// <summary>The way they die of it: as of her hand's burn (EmpressHeld).</summary>
        private const string SeenThrough = " was seen through.";

        private bool Burns => !Player.dead && EmpressOfLight.InHeldDay(Player.Hitbox);

        public override void UpdateBadLifeRegen()
        {
            if (!Burns)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= EmpressDayCreature.LifeASecond * 2;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (Burns)
                damageSource = PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Player.name + SeenThrough));

            return true;
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (!Burns)
                return;

            fullBright = true;
            r = g = b = 1f;
            EmpressOfLight.DayLight(Player.Center, Player.Size);
        }
    }
}
