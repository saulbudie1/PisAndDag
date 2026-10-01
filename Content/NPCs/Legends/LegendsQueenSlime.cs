using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The Queen Slime.
    ///
    /// IN EVERY WORLD she is made as gelatinous as the King: her whole body a spring that
    /// squashes, stretches, leans and ripples with what she does, drawn a fifth see-through
    /// over vanilla's own shader for her gel, her crown riding her and hopping off her head
    /// when she lands, her gel shaken off her in globs. Her slam is given a tell, a launch, a
    /// hang and an impact the ground feels (LegendsQueenSlime.Look). And three scenes are hers:
    ///
    ///   THE ARRIVAL (Rite)   the Gelatin Crystal flies up out of the hand that raised it; the
    ///                        camera follows it and leans in; it cracks, three times, shaking;
    ///                        it bursts into white light the colours of her; the light runs,
    ///                        weightless, into the shape of her core, and hangs there two
    ///                        seconds; she swells out of it, her crown drops onto her, she
    ///                        falls to the ground and roars, and her name comes up.
    ///   THE WINGS (Wings)    at half her life her wings come out of her back folded, shoot
    ///                        up, spread wide and big, and she takes off: the screen and the
    ///                        ground shake, the dust goes up with her. From then on she is
    ///                        never more than half (her second half, winged, for good).
    ///   THE DEATH (Death)    she spasms, her wings break into splinters, she falls; for
    ///                        eight seconds she hurls herself about in slams near and onto
    ///                        the player; then she stops, shudders three seconds, and bursts,
    ///                        and every slime of hers pops with her.
    ///
    /// Her slimes (QueenSlimeMinions) last thirty seconds, then pop, and give nothing. And while
    /// she arrives, anything hostile near her with less than 500 life is killed on the spot,
    /// and gives nothing either (Rite, ClearScene).
    ///
    /// IN LEGENDS she is also harder: a fifth again her life and defense, a tenth again her
    /// own blows and her gel (not her minions'); and she has two moves more (LegendsQueenSlime.Moves): on the
    /// ground her core, thrown spinning and glowing red, out through where the player stands
    /// and round like a boomerang half a screen wide; in the air a dash level with the player,
    /// as the Queen Bee does, that takes whoever it meets inside her for four seconds.
    ///
    /// Vanilla's own AI (AI_121) fights her the rest of the time, untouched. The scenes and the
    /// moves take the frame instead of it (PreAI false) and hand her back to it at rest.
    ///
    /// The server (or a single player) decides every scene and move and sends where it is
    /// (SendExtraAI); every machine plays it from that, and counts its clock on by itself
    /// between packets. Everything seen or heard is played on every machine; nothing that
    /// moves her or hurts anyone is decided anywhere but the server.
    /// </summary>
    public partial class LegendsQueenSlime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.QueenSlimeBoss;

        // ---- stats, Legends -------------------------------------------------------------------------

        /// <summary>Life, contact damage and defense, on top of vanilla's (before its difficulty scaling).</summary>
        private const float QueenLife = 1.2f;
        private const float QueenDamage = 1.1f;
        private const float QueenDefense = 1.2f;

        /// <summary>Her gel and her slam's shockwave: as her blows, a tenth again (LegendsQueenSlimeShots).</summary>
        internal const float QueenShots = 1.1f;

        public override void SetDefaults(NPC npc)
        {
            if (!PADWorld.Legends)
                return;

            npc.lifeMax = (int)(npc.lifeMax * QueenLife);
            npc.damage = (int)(npc.damage * QueenDamage);
            npc.defense = (int)Math.Round(npc.defense * QueenDefense);
        }

        // ---- the scene ---------------------------------------------------------------------------------

        /// <summary>What has her, instead of vanilla: nothing, one of her three scenes, or a move of Legends.</summary>
        internal enum Scene : byte
        {
            None,
            Rite,
            Wings,
            Death,
            Boomerang,
            Dash,
        }

        private Scene scene;

        /// <summary>The step of the scene, and ticks into it.</summary>
        private int beat;
        private int timer;

        /// <summary>The scene and step this machine last played the start of, and the tick it last played.</summary>
        private int seenKey = -1;
        private int lastTick = -1;

        /// <summary>The scene this machine ran last tick: when the server ends one, this is how a client knows to put her back.</summary>
        private Scene played;

        /// <summary>The server, or a single player: whoever decides.</summary>
        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>Contact damage as built, once difficulty scaling is done: the scenes zero it and put it back.</summary>
        private int baseDamage = -1;

        /// <summary>Her wings are out: she has been through them, and they have not been broken.</summary>
        private bool transitioned;

        /// <summary>Latched by CheckDead; the death owns her from then. And its end, when the death is let through.</summary>
        private bool dying;
        private bool ending;

        private bool wingsBroken;

        /// <summary>Her core is out of her, thrown (Legends).</summary>
        private bool coreOut;

        /// <summary>The side she lines up on for a move, +1 right of the player, -1 left.</summary>
        private int side = 1;

        /// <summary>The last move of Legends she made, so none comes twice running.</summary>
        private Scene lastMove;

        /// <summary>What vanilla's state was when its AI last started a tick: a new move is one it has just picked.</summary>
        private float aiStateBefore;

        /// <summary>Half her life: the wings are due.</summary>
        private static bool Halved(NPC npc) => npc.life <= npc.lifeMax / 2;

        /// <summary>The Queen at this index, if there is one.</summary>
        public static LegendsQueenSlime Of(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];

            if (!npc.active || npc.type != NPCID.QueenSlimeBoss)
                return null;

            return npc.TryGetGlobalNPC(out LegendsQueenSlime queen) ? queen : null;
        }

        /// <summary>On to this scene, at this step, from its first tick. The server's call, and sent.</summary>
        private void Go(NPC npc, Scene to, int step)
        {
            scene = to;
            beat = step;
            timer = -1;
            npc.netUpdate = true;
        }

        /// <summary>On to the next step of this scene. The server's call, and sent.</summary>
        private void Next(NPC npc, int step)
        {
            beat = step;
            timer = -1;
            npc.netUpdate = true;
        }

        /// <summary>This machine is at the start of its step: true once, the first tick it sees it.</summary>
        private bool Entered()
        {
            int key = (int)scene * 64 + beat;

            if (key == seenKey)
                return false;

            seenKey = key;
            lastTick = -1;
            return true;
        }

        /// <summary>The clock went past this mark since the last tick: true once, wherever a packet moved it.</summary>
        private bool Crossed(int mark) => lastTick < mark && timer >= mark;

        /// <summary>Back to vanilla, at rest: its state reset so it picks its next move in its own time. The server's call, and sent.</summary>
        private void Release(NPC npc)
        {
            Scene ended = scene;

            scene = Scene.None;
            beat = 0;
            timer = 0;

            Restore(npc, ended);

            if (Authority)
            {
                npc.ai[0] = 0f;
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// What a scene did to her, undone, on this machine: the server's when it ends one
        /// (Release), a client's the first tick it is told the scene is over. And the camera
        /// walked home for a player who was watching.
        /// </summary>
        private void Restore(NPC npc, Scene ended)
        {
            played = Scene.None;
            seenKey = -1;
            lastTick = -1;

            npc.dontTakeDamage = false;
            npc.damage = baseDamage;
            npc.hide = false;
            npc.chaseable = true;
            npc.rotation = 0f;
            dashStretch = 0f;
            grabbed = -1;

            if (ended == Scene.Rite)
                EndRite(npc);
        }

        // ---- ai ------------------------------------------------------------------------------------

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            // The server's: where the crystal goes and where she forms. The clients are told.
            if (!Authority)
                return;

            Player summoner = source is EntitySource_BossSpawn spawn && spawn.Target is Player p ? p : null;

            if (summoner == null)
            {
                npc.TargetClosest(false);
                summoner = Main.player[npc.target];
            }

            if (summoner == null || !summoner.active || summoner.dead)
                return;

            BeginRite(npc, summoner);
        }

        public override bool PreAI(NPC npc)
        {
            if (baseDamage < 0)
                baseDamage = npc.damage;

            aiStateBefore = npc.ai[0];

            // Winged, she stays in her second half for good. Vanilla picks its half from her
            // life every tick (AI_121), so anything that healed her past half (her dash, say)
            // would put her back on the ground, hopping, with her wings still on.
            if (transitioned && !dying && npc.life > npc.lifeMax / 2)
                npc.life = npc.lifeMax / 2;

            // A scene is over (the server ended it, or went straight on to another, as her death
            // does out of a move): what it did to her undone here too, before anything else.
            if (played != Scene.None && played != scene)
                Restore(npc, played);

            if (scene == Scene.None)
            {
                // Half her life: the wings, in every world, before anything else of vanilla's.
                if (!Authority || transitioned || dying || !Halved(npc))
                    return true;

                StartWings(npc);
            }

            played = scene;

            // A scene has her: it is never let go for want of a player.
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

            // Once, the first tick this machine sees each step: everything a step starts with
            // is played from this, and its marks are counted from it.
            bool entered = Entered();

            switch (scene)
            {
                case Scene.Rite:
                    RunRite(npc, entered);
                    break;

                case Scene.Wings:
                    RunWings(npc, entered);
                    break;

                case Scene.Death:
                    RunDeath(npc, entered);
                    break;

                case Scene.Boomerang:
                    RunBoomerang(npc, entered);
                    break;

                case Scene.Dash:
                    RunDash(npc, entered);
                    break;
            }

            // Her own light, which vanilla's AI gives her and a scene would otherwise take away:
            // not while she is still only light (her arrival) or gone (her death).
            bool shown = !(scene == Scene.Rite && beat < RiteGrow) && !(scene == Scene.Death && beat == DeathBurst);

            if (scene != Scene.None && shown)
                Lighting.AddLight(npc.Center, 1f, 0.7f, 0.9f);

            lastTick = timer;
            timer++;

            return false;
        }

        public override void PostAI(NPC npc)
        {
            // Her moves of Legends: in place of one vanilla has just picked. The server's.
            if (Authority && scene == Scene.None && PADWorld.Legends)
                ConsiderMove(npc);

            if (!Main.dedServ)
                Animate(npc);
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (scene == Scene.Rite || scene == Scene.Wings)
                return false;

            // Dying, she hurts only while she throws herself about.
            if (scene == Scene.Death && beat != DeathFrenzy)
                return false;

            // Already inside her: contact on top of the drain would be charging twice.
            return !target.GetModPlayer<QueenSlimeGrab>().Held;
        }

        /// <summary>
        /// Refuses her first death and hands her to her own: life pinned at one, untouchable.
        /// The death zeroes her life itself at its end and calls checkDead again, and that one
        /// is let through: the tick her loot drops.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (ending)
                return true;

            if (!dying)
                BeginDeath(npc);

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;

            return false;
        }

        // ---- sync ----------------------------------------------------------------------------------
        //
        // The scene, its step and its clock, where the arrival is staged, and what she has lost.
        // A packet moves this machine's clock only when the scene or its step has changed or the
        // clock has drifted.

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(transitioned);
            bitWriter.WriteBit(dying);
            bitWriter.WriteBit(wingsBroken);
            bitWriter.WriteBit(coreOut);

            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)beat);
            binaryWriter.Write((short)Math.Clamp(timer, short.MinValue, short.MaxValue));
            binaryWriter.WriteVector2(riteStart);
            binaryWriter.WriteVector2(riteFocus);
            binaryWriter.Write(riteGround);
            binaryWriter.Write((sbyte)side);
            binaryWriter.Write((byte)lastMove);
            binaryWriter.Write((byte)(grabbed < 0 ? byte.MaxValue : grabbed));
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            transitioned = bitReader.ReadBit();
            bool wasDying = bitReader.ReadBit();
            wingsBroken |= bitReader.ReadBit();
            coreOut = bitReader.ReadBit();

            Scene got = (Scene)binaryReader.ReadByte();
            int step = binaryReader.ReadByte();
            int clock = binaryReader.ReadInt16();

            riteStart = binaryReader.ReadVector2();
            riteFocus = binaryReader.ReadVector2();
            riteGround = binaryReader.ReadSingle();
            side = binaryReader.ReadSByte();
            lastMove = (Scene)binaryReader.ReadByte();

            int held = binaryReader.ReadByte();
            grabbed = held == byte.MaxValue ? -1 : held;

            // The server's word, over whatever this machine guessed from a blow of its own.
            dying = wasDying;

            if (got != scene || step != beat || Math.Abs(clock - timer) > 6)
            {
                scene = got;
                beat = step;
                timer = clock;
            }

            // Told of a scene before its first tick here: untouchable from now, so nothing takes
            // her for fighting in the meantime.
            if (scene == Scene.Rite || scene == Scene.Wings || scene == Scene.Death)
                npc.dontTakeDamage = true;
        }
    }

    /// <summary>Her gel and her slam's shockwave, a tenth again in Legends, as her blows are. Not her minions'.</summary>
    public class LegendsQueenSlimeShots : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
            entity.type == ProjectileID.QueenSlimeGelAttack || entity.type == ProjectileID.QueenSlimeSmash;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends)
                return;

            if (source is EntitySource_Parent parent && parent.Entity is NPC npc && npc.type == NPCID.QueenSlimeBoss)
                projectile.damage = (int)Math.Round(projectile.damage * LegendsQueenSlime.QueenShots);
        }
    }

    /// <summary>
    /// Her arrival and her name are her own (the plate waits for her roar), her art is loaded
    /// with the mod, and whoever she holds is drawn inside her (QueenSlimeGrab.DrawHeld, after
    /// the players, since she is drawn before them).
    /// </summary>
    public class LegendsQueenSlimeSystem : ModSystem
    {
        public override void Load()
        {
            QueenSlimeArt.Load();

            On_NPC.VanillaHitEffect += LegendsQueenSlime.HitNotKilled;

            if (!Main.dedServ)
                On_Main.DrawPlayers_AfterProjectiles += DrawAfterPlayers;
        }

        public override void Unload()
        {
            QueenSlimeArt.Unload();
        }

        public override void PostSetupContent()
        {
            BossPlate.HasOwnIntro(NPCID.QueenSlimeBoss);
            BossPlate.NamesItself(NPCID.QueenSlimeBoss);
            BossPlate.BarWaitsForName(NPCID.QueenSlimeBoss);
        }

        private static void DrawAfterPlayers(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self)
        {
            orig(self);
            LegendsQueenSlime.DrawHolding();
        }
    }
}
