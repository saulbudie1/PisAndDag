using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// Terraprisma's opening cut, by request: "Make the first attack of terraprisma after in idle position be: teleport
    /// from idle position to behind the targeted npc, leaving a streak of slash that explode that deals double damage".
    ///
    /// A blade waiting at its place behind the player (the game's state 0, Projectile.AI_156_Think) picks a target now
    /// and then; the first attack it then begins is always its dive (the state set to 80: rise, fall through the target,
    /// out the far side). The tick it begins it, the blade is not there to rise: it is already behind the target, on the
    /// far side from where it waited (as far past the target's middle as half the target's size and 40 pixels more),
    /// turned back to face it, and where it was is a cut through the air from there to here (PrismCut), which hits as it
    /// is made and bursts a moment later for twice the blade's damage. The blade waits there, behind the target and
    /// keeping with it as it moves, until the cut bursts (by request: "unless streak is exploded, it doesnt attack"):
    /// the game's own working of it is let be for those ticks, but for what keeps a blade alive and lit. Then the dive the
    /// game goes on with starts from behind the target, and comes back through it.
    ///
    /// Worked out on its owner's machine (where the blade's choice of target is the one kept), the cut made there and sent
    /// to everyone, the blade's new place and its wait sent with it (SendExtraAI), so that it waits on every machine.
    /// </summary>
    public class TerraprismaOpener : GlobalProjectile
    {
        /// <summary>What the game sets a blade's state to as it begins its dive from waiting (Projectile.AI_156_Think: 40 + 40).</summary>
        private const float Dive = 80f;

        /// <summary>How far past the target's edge it comes out.</summary>
        private const float Past = 40f;

        /// <summary>The blade was waiting at its place when its tick began.</summary>
        private bool waiting;

        /// <summary>Ticks left that it holds behind its target for its cut to burst; whom, and where from them.</summary>
        private int hold, holdOn = -1;
        private Vector2 holdAt;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.EmpressBlade;

        public override bool PreAI(Projectile projectile)
        {
            if (hold > 0)
            {
                hold--;
                waiting = false;
                Hold(projectile);
                return false;
            }

            waiting = projectile.ai[0] == 0f;
            return true;
        }

        /// <summary>A tick of waiting behind the target: kept alive and lit as the game keeps it (Projectile.AI_156_BatOfLight), kept with the target, turned to it.</summary>
        private void Hold(Projectile projectile)
        {
            Player player = Main.player[projectile.owner];

            if (player.dead)
                player.empressBlade = false;

            if (player.empressBlade)
                projectile.timeLeft = 2;

            Lighting.AddLight(projectile.Center, projectile.AI_156_GetColor().ToVector3());
            projectile.velocity = Vector2.Zero;

            if (Main.npc.IndexInRange(holdOn) && Main.npc[holdOn].active)
            {
                NPC npc = Main.npc[holdOn];
                projectile.Center = npc.Center + holdAt;
                projectile.rotation = projectile.rotation.AngleLerp((npc.Center - projectile.Center).ToRotation() + MathHelper.PiOver2, 0.3f);
            }
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)hold);
            binaryWriter.Write((short)holdOn);
            binaryWriter.Write(holdAt.X);
            binaryWriter.Write(holdAt.Y);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            hold = binaryReader.ReadByte();
            holdOn = binaryReader.ReadInt16();
            holdAt = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
        }

        public override void PostAI(Projectile projectile)
        {
            if (!waiting || projectile.ai[0] != Dive || projectile.owner != Main.myPlayer)
                return;

            int target = (int)projectile.ai[1];

            if (!Main.npc.IndexInRange(target) || !Main.npc[target].active)
                return;

            NPC npc = Main.npc[target];
            Vector2 from = projectile.Center;
            Vector2 way = (npc.Center - from).SafeNormalize(Vector2.UnitX);
            Vector2 to = npc.Center + way * (System.Math.Max(npc.width, npc.height) * 0.5f + Past);
            Color hue = projectile.AI_156_GetColor();

            Projectile.NewProjectile(projectile.GetSource_FromThis(), from, Vector2.Zero, ModContent.ProjectileType<PrismCut>(),
                projectile.damage, projectile.knockBack, projectile.owner, to.X, to.Y, PrismCut.Pack(hue));

            // It waits there until the cut bursts (the cut's first tick may be this one or the next).
            hold = PrismCut.Fuse + 1;
            holdOn = target;
            holdAt = to - npc.Center;

            projectile.Center = to;
            projectile.velocity = Vector2.Zero;
            projectile.rotation = (npc.Center - to).ToRotation() + MathHelper.PiOver2;

            for (int i = 0; i < projectile.oldPos.Length; i++)
            {
                projectile.oldPos[i] = projectile.position;
                projectile.oldRot[i] = projectile.rotation;
            }

            projectile.netUpdate = true;
        }
    }
}
