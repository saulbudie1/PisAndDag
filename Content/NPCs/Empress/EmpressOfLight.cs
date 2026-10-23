using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The Empress of Light, Elin. In every world, by request, she comes and goes as a scene, and is
    /// fought in an arena of her own:
    ///
    ///   HER COMING (Arrival)   the Prismatic Lacewing is struck down, and everything stops: the
    ///                          player held, the music and the world's sounds gone, the light
    ///                          flickering, the moon going dark and bright and the sky pitch and
    ///                          back; then a darker sky, hers. She comes as she always has, over
    ///                          the player; flashes down onto them, her middle on theirs; a hexagon
    ///                          of light shuts round them both, and drags them up to halfway between
    ///                          the ground and the top of the sky. Ten stars spin out of her and
    ///                          round her, a second of it, and shoot out to the ten corners of a
    ///                          decagon (125 tiles off in a normal world, 115 in an expert one, 100
    ///                          in a master one, 15 fewer in Legends: EmpressArena.Radius); the camera goes with one and
    ///                          sees the border drawn between them, and what lies outside it come
    ///                          (a decagon, by request: ten stars, a corner each). The cage breaks; the player falls,
    ///                          and her favour catches them; she rises to five hundred pixels over
    ///                          them, and screams, and her name comes up: Elin, Empress Of Light.
    ///                          Then the music.
    ///   THE ARENA              EmpressArena: solid walls for whoever is in it, dash, teleport or
    ///                          not (by request), its lower edges a floor to stand on; and her favour
    ///                          (Favor of an Empress), flight without end at the speed of solar
    ///                          wings, until they die or she does.
    ///   HER DEATH (Death)      the blow that would kill her stops her instead: she slows, over the
    ///                          player held still in the air; the camera goes to her; light comes out
    ///                          of her eyes, and cracks run through her, body and arms and legs, with
    ///                          light shining out of them, while she speaks. The border closes in, and
    ///                          the world shakes, three seconds of it; then it is thrown out and gone,
    ///                          and she gets smaller and smaller, and is butterflies: twenty real ones,
    ///                          thrown out every way (EmpressButterflies). Her things fall where she
    ///                          was. Nothing of her body is left.
    ///
    /// Vanilla's own AI (AI_120) fights her the rest of the time, untouched. The scenes take the frame
    /// instead of it (PreAI false) and hand her back to it ready to choose her first move.
    ///
    /// The server (or a single player) decides every step and where; every machine plays it from
    /// that (SendExtraAI), and counts its clock on by itself between packets. Everything seen or
    /// heard is played on every machine; nothing that moves her, or anybody, is decided anywhere but
    /// the machine whose it is to decide.
    /// </summary>
    public partial class EmpressOfLight : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.HallowBoss;

        // ---- her numbers ---------------------------------------------------------------------------------

        /// <summary>Her defense in every world, a quarter again over vanilla's (by request, "Buff her natural defense by 25%"): 50 to 63.</summary>
        public const float DefenseScale = 1.25f;

        /// <summary>
        /// In Legends, on top of that (by request, "life * 1.65f / defense *1.15 / damage * 1.2"): her life, her defense (63 to
        /// 72), and the blow of her body and of everything she throws (EmpressShots).
        /// </summary>
        public const float LegendsLife = 1.65f;
        public const float LegendsDefense = 1.15f;
        public const float LegendsDamage = 1.2f;

        public override void SetDefaults(NPC npc)
        {
            // Before the world's own scaling (NPC.SetDefaults), so each is on top of the usual: her defense and her
            // body's blow become what vanilla works from each tick (AI_120 sets both from her defDefense and defDamage,
            // her change's defense too), and her life is then scaled for the world as anyone's is.
            float defense = npc.defense * DefenseScale;

            if (PADWorld.Legends)
            {
                defense *= LegendsDefense;
                npc.lifeMax = (int)(npc.lifeMax * LegendsLife);
                npc.damage = (int)(npc.damage * LegendsDamage);
            }

            npc.defense = (int)Math.Round(defense, MidpointRounding.AwayFromZero);
        }

        // ---- the scene ---------------------------------------------------------------------------------

        /// <summary>What has her, instead of vanilla: nothing, her coming, or her death; in Legends, one of her acts, her grab, or her change.</summary>
        internal enum Scene : byte
        {
            None,
            Arrival,
            Death,
            Act,
            Grab,
            Change,
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

        /// <summary>Latched by CheckDead; the death owns her from then. And its end, when the death is let through.</summary>
        private bool dying;
        private bool ending;

        // ---- the arena ----------------------------------------------------------------------------------

        /// <summary>Where her arena stands, once it does: set by her coming, or round whoever she first fights.</summary>
        private bool arenaSet;
        private Vector2 arenaCentre;

        /// <summary>The player she came for, and was summoned by.</summary>
        private int summoner = -1;

        /// <summary>The Empress at this index, if there is one.</summary>
        public static EmpressOfLight Of(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];

            if (!npc.active || npc.type != NPCID.HallowBoss)
                return null;

            return npc.TryGetGlobalNPC(out EmpressOfLight empress) ? empress : null;
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

        /// <summary>Back to vanilla, ready to choose her first move. The server's call, and sent.</summary>
        private void Release(NPC npc)
        {
            Scene ended = scene;

            scene = Scene.None;
            beat = 0;
            timer = 0;

            Restore(npc, ended);

            if (Authority)
            {
                // Vanilla's coming (its state 0) is done: on to choosing, at once.
                npc.ai[0] = 1f;
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
                npc.TargetClosest(false);
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// What a scene did to her, undone, on this machine: the server's when it ends one (Release),
        /// a client's the first tick it is told the scene is over. And the camera walked home for a
        /// player who was watching.
        /// </summary>
        private void Restore(NPC npc, Scene ended)
        {
            played = Scene.None;
            seenKey = -1;
            lastTick = -1;

            npc.dontTakeDamage = false;
            npc.damage = ContactDamage(npc);
            npc.chaseable = true;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.Opacity = 1f;
            npc.rotation = 0f;

            if (ended == Scene.Arrival)
                EndArrival(npc);

            if (ended == Scene.Grab)
                grabbed = -1;
        }

        // ---- ai ---------------------------------------------------------------------------------------

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            // The server's: whom she came for. The clients are told.
            if (!Authority)
                return;

            Player who = source is EntitySource_BossSpawn spawn && spawn.Target is Player p ? p : null;

            if (who == null)
            {
                npc.TargetClosest(false);
                who = Main.player[npc.target];
            }

            if (who == null || !who.active || who.dead)
                return;

            BeginArrival(npc, who);
        }

        public override bool PreAI(NPC npc)
        {
            // A scene is over (the server ended it, or went straight on to another): what it did to her
            // undone here too, before anything else.
            if (played != Scene.None && played != scene)
                Restore(npc, played);

            // Come some other way than her own (a scene skipped for want of a player): her arena stands
            // round whoever she fights first.
            if (Authority && !arenaSet && scene == Scene.None)
            {
                npc.TargetClosest(false);
                Player first = Main.player[npc.target];

                if (first.active && !first.dead)
                {
                    arenaCentre = ArenaCentre(first.Center);
                    arenaSet = true;
                    npc.netUpdate = true;
                }
            }

            // The arena, and her sky, on every machine, every tick she has them.
            KeepArena(npc);

            // Her bow at rest unless something has her hold it otherwise this tick (her second half in Legends); and
            // whether she is drawn as her second half.
            BowRest();
            secondDrawn = SecondLook(npc);

            // A death this machine dealt and the server has not yet answered: lapses if it never does.
            if (heldFor > 0)
            {
                if (scene != Scene.None || dying)
                    heldFor = 0;
                else if (--heldFor == 0)
                    npc.dontTakeDamage = false;
            }

            // Her sky, through the fight, for whoever is in it or near it: between her acts and through them (her coming,
            // her change and her death ask for it their own way).
            if (arenaSet && (scene == Scene.None || scene == Scene.Act || scene == Scene.Grab) && NearArena())
                EmpressSky.Darken(1f);

            if (scene == Scene.None)
            {

                // In Legends, her first half: every other move one of her acts (EmpressOfLight.Legends); and her second:
                // every other move one of its own (EmpressOfLight.Second).
                if (LegendsChoose(npc) || SecondChoose(npc))
                {
                    timer = 0;
                    return false;
                }

                return true;
            }

            played = scene;

            // A scene has her: it is never let go for want of a player.
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

            // Once, the first tick this machine sees each step: everything a step starts with is
            // played from this, and its marks are counted from it.
            bool entered = Entered();

            switch (scene)
            {
                case Scene.Arrival:
                    RunArrival(npc, entered);
                    break;

                case Scene.Death:
                    RunDeath(npc, entered);
                    break;

                case Scene.Act:
                    RunAct(npc, entered);
                    break;

                case Scene.Grab:
                    RunGrab(npc, entered);
                    break;

                case Scene.Change:
                    RunChange(npc, entered);
                    break;
            }

            // Her wings beat on, as vanilla's AI would have them; and her own light, as vanilla gives her.
            if (++npc.localAI[0] >= 44f)
                npc.localAI[0] = 0f;

            if (npc.Opacity > 0f)
                Lighting.AddLight(npc.Center, Vector3.One * npc.Opacity);

            lastTick = timer;
            timer++;

            return false;
        }

        /// <summary>The arena as her scene of the moment has it, on every machine: up, and how, or not yet.</summary>
        private void KeepArena(NPC npc)
        {
            if (!arenaSet)
                return;

            switch (scene)
            {
                case Scene.None:
                case Scene.Act:
                case Scene.Grab:
                case Scene.Change:
                    EmpressArena.Keep(arenaCentre, EmpressArena.Radius, true, true);
                    break;

                case Scene.Arrival:
                    ArrivalArena(npc);
                    break;

                case Scene.Death:
                    DeathArena(npc);
                    break;
            }
        }

        /// <summary>Vanilla's AI lets her be struck again whenever it likes: not while a death dealt here waits on the server's word.</summary>
        public override void PostAI(NPC npc)
        {
            Talk(npc);
            LegendsAfter(npc);
            SecondAfter(npc);
            NegativeTick(npc);
            BowTick(npc);
            StepSeen(npc);

            if (heldFor > 0 && scene == Scene.None)
                npc.dontTakeDamage = true;
        }

        /// <summary>
        /// Her body hurts through the fight and her acts as vanilla's does; not through her scenes, nor her grab, nor the
        /// mirage's strike (whose blow is the grab); nor, in Legends, her dash, which takes instead of striking.
        /// </summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (scene == Scene.Act)
                return act != Act.Mirage;

            if (scene != Scene.None)
                return false;

            return !((LegendsHalf(npc) || LegendsSecond(npc)) && (npc.ai[0] == 8f || npc.ai[0] == 9f));
        }

        /// <summary>
        /// This machine's player is in her arena or near enough to it to see it: her sky is theirs. (Further
        /// off, the world is as it was, sky and all.)
        /// </summary>
        private bool NearArena()
        {
            if (Main.dedServ || !arenaSet)
                return false;

            Player me = Main.LocalPlayer;
            return me.active && Vector2.Distance(me.Center, arenaCentre) <= EmpressArena.Reach;
        }

        /// <summary>
        /// Ticks a death dealt by a blow of this machine's own waits on the server to say so, before she is
        /// let be struck again here: about two round trips at their worst.
        /// </summary>
        private const int HeldTicks = 120;
        private int heldFor;

        /// <summary>
        /// Refuses her first death and hands her to her own: life pinned at one, untouchable. The death
        /// zeroes her life itself at its end and calls checkDead again, and that one is let through: the
        /// tick her loot drops.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (ending)
                return true;

            // A client's own blow (vanilla runs checkDead where the blow lands, the server too when it
            // hears of it): her death is the server's to begin, and it says so. A death begun here would
            // be undone by any packet the server sent before it heard of the blow, and begun again after.
            // Until it says, she is held at one life and out of the fight here, so no more blows go to a
            // death already dealt; if it never says, that lapses (PreAI).
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (!dying && scene == Scene.None)
                    heldFor = HeldTicks;

                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.active = true;
                return false;
            }

            if (!dying)
                BeginDeath(npc);

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;

            return false;
        }

        /// <summary>
        /// Handed back to vanilla to die. CheckDead lets this one through: the loot. Her sound was the
        /// scene's, so vanilla's is not played again; and every client is told she is gone, as nothing
        /// struck her.
        /// </summary>
        private void End(NPC npc)
        {
            ending = true;
            npc.DeathSound = null;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>
        /// The blow that would kill her only starts her death (CheckDead refuses it), but vanilla shows
        /// the blow before that is asked, and shows it as her death: her pieces thrown off her and a
        /// burst of her dust (NPC.StrikeNPC runs HitEffect, then checkDead). By request there are no
        /// pieces of her ("no gore"): while her death is still to be refused, the blow is shown the way
        /// vanilla shows one she lives through; and at the end nothing is shown at all.
        /// </summary>
        internal static void HitNotKilled(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            if (self.type != NPCID.HallowBoss || self.life > 0 || !self.TryGetGlobalNPC(out EmpressOfLight empress))
            {
                orig(self, hitDirection, dmg, instantKill);
                return;
            }

            // Her end: she is a butterfly by now, and nothing of her body is left to throw.
            if (empress.ending)
                return;

            int life = self.life;
            self.life = 1;

            try
            {
                orig(self, hitDirection, dmg, instantKill);
            }
            finally
            {
                self.life = life;
            }
        }

        // ---- where her arena stands ----------------------------------------------------------------------------

        /// <summary>The highest a player can be (Player.Update keeps them below this), in world pixels.</summary>
        private static float Ceiling => Main.topWorld + 656f;

        /// <summary>
        /// Her arena's middle for a fight at <paramref name="near"/>: by request, "the middle of the
        /// ground and sky's ceiling", over the ground there; and far enough in from the world's sides
        /// that all of it is in the world.
        /// </summary>
        internal static Vector2 ArenaCentre(Vector2 near)
        {
            float edge = 41f * 16f + EmpressArena.Radius + 32f;
            float x = MathHelper.Clamp(near.X, Main.leftWorld + edge, Math.Max(Main.leftWorld + edge, Main.rightWorld - edge));

            float ground = GroundUnder(x, near.Y);
            float y = (ground + Ceiling) * 0.5f;

            return new Vector2(x, y);
        }

        /// <summary>
        /// The ground at this x: the top of the first solid ground going down from the sky that is not
        /// an island in it (one with open sky under it); under the surface, no deeper than the surface.
        /// </summary>
        private static float GroundUnder(float worldX, float nearY)
        {
            int x = Math.Clamp((int)(worldX / 16f), 1, Main.maxTilesX - 2);
            int top = (int)(Ceiling / 16f) + 2;
            int surface = (int)Main.worldSurface;

            for (int y = top; y < surface; y++)
            {
                if (!WorldGen.SolidTile(x, y))
                    continue;

                // An island: solid for a while, then sky under it. Gone past.
                int below = y;

                while (below < surface && WorldGen.SolidTile(x, below))
                    below++;

                int open = 0;

                for (int k = below; k < Math.Min(surface, below + 40); k++)
                {
                    if (WorldGen.SolidTile(x, k))
                        break;

                    open++;
                }

                if (open >= 24 && below + open < surface)
                {
                    y = below + open - 1;
                    continue;
                }

                return y * 16f;
            }

            // No ground before the underground: as low as the surface goes, or where the player stood.
            return Math.Min(surface * 16f, Math.Max(nearY, Ceiling + 600f));
        }

        // ---- sync ---------------------------------------------------------------------------------------
        //
        // The scene, its step and its clock, where things are staged, and her arena. A packet moves this
        // machine's clock only when the scene or its step has changed or the clock has drifted.

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(dying);
            bitWriter.WriteBit(arenaSet);

            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)beat);
            binaryWriter.Write((short)Math.Clamp(timer, short.MinValue, short.MaxValue));
            binaryWriter.Write((byte)(summoner < 0 ? byte.MaxValue : summoner));
            binaryWriter.WriteVector2(arenaCentre);
            binaryWriter.WriteVector2(spawnAt);
            binaryWriter.WriteVector2(cageFrom);
            binaryWriter.Write((short)dragTicks);
            binaryWriter.WriteVector2(deathAt);

            WriteLegends(binaryWriter);
            WriteSecond(binaryWriter);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool wasDying = bitReader.ReadBit();
            arenaSet = bitReader.ReadBit();

            Scene got = (Scene)binaryReader.ReadByte();
            int step = binaryReader.ReadByte();
            int clock = binaryReader.ReadInt16();

            int who = binaryReader.ReadByte();
            summoner = who == byte.MaxValue ? -1 : who;

            arenaCentre = binaryReader.ReadVector2();
            spawnAt = binaryReader.ReadVector2();
            cageFrom = binaryReader.ReadVector2();
            dragTicks = binaryReader.ReadInt16();
            deathAt = binaryReader.ReadVector2();

            ReadLegends(binaryReader);
            ReadSecond(binaryReader);

            // The server's word, over whatever this machine guessed from a blow of its own.
            dying = wasDying;

            if (got != scene || step != beat || Math.Abs(clock - timer) > 6)
            {
                scene = got;
                beat = step;
                timer = clock;
            }

            // Told of a scene before its first tick here: untouchable from now, so nothing takes her
            // for fighting in the meantime. (Her acts and her grab are fought through.)
            if (scene == Scene.Arrival || scene == Scene.Death || scene == Scene.Change)
                npc.dontTakeDamage = true;
        }
    }

    /// <summary>
    /// Her coming and her name are her own (the plate waits for her scream, in her colours), her face
    /// is on the panel she speaks from (the mod's copy of her picture, EmpressTextures), and what goes
    /// over the player (her cage, her stars in flight) is drawn after the players, since she is drawn
    /// before them.
    /// </summary>
    public class EmpressOfLightSystem : ModSystem
    {
        /// <summary>Her name and title on the plate, by request, and their colours: Elin #ff87dd, Empress Of Light #badfff.</summary>
        public const string HerName = "Elin";
        public const string HerTitle = "Empress Of Light";

        /// <summary>Her face on the panel: the game's picture of her, the frame round her head.</summary>
        private static readonly Rectangle Face = new Rectangle(66, 27, 34, 34);

        public override void Load()
        {
            EmpressArt.Load();

            On_NPC.VanillaHitEffect += EmpressOfLight.HitNotKilled;

            if (!Main.dedServ)
                On_Main.DrawPlayers_AfterProjectiles += DrawAfterPlayers;
        }

        public override void Unload()
        {
            EmpressArt.Unload();
            EmpressOfLight.UnloadNegative();
        }

        public override void PostSetupContent()
        {
            BossPlate.Register(NPCID.HallowBoss, HerName, HerTitle);
            BossPlate.Colours(NPCID.HallowBoss, EmpressArt.Pink, EmpressArt.Sky);
            BossPlate.HasOwnIntro(NPCID.HallowBoss);
            BossPlate.NamesItself(NPCID.HallowBoss);
            BossPlate.BarWaitsForName(NPCID.HallowBoss);

            // Her face from the mod's own copy of her (EmpressTextures), so a repainted face is hers on the panel too.
            DialoguePortrait.Register(NPCID.HallowBoss, "PisAndDag/Content/NPCs/Legends/Empress/Body", Face);
            DialoguePortrait.Name(NPCID.HallowBoss, HerName, HerTitle);
        }

        private static void DrawAfterPlayers(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self)
        {
            orig(self);
            EmpressOfLight.DrawOverPlayers();
        }
    }

    /// <summary>
    /// Everything she throws, in Legends, a fifth again as hard (EmpressOfLight.LegendsDamage): vanilla's bolts, lances,
    /// sun dance and rainbows, and her acts' own (EmpressHitbox). Where it is made (the server, or the one player); the
    /// rest are sent it. By day, as everything of hers, it kills, and that is left as it is.
    /// </summary>
    public class EmpressShots : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends || projectile.damage <= 0 || projectile.damage >= 9999)
                return;

            // Hers only: what comes of hers (the pieces of a shot) has its damage from her shot already.
            if (source is EntitySource_Parent parent && parent.Entity is NPC npc && npc.type == NPCID.HallowBoss)
                projectile.damage = (int)Math.Round(projectile.damage * EmpressOfLight.LegendsDamage);
        }
    }
}
