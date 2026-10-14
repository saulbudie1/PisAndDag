using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// The squad's Giant Cursed Skull (NPC 289): it flies through everything, as the game's does
    /// (aiStyle 10 as it is in 1.4.4): drifting at what it goes for, faster the farther it is,
    /// weaving about it when close, and every ten seconds a short rush at it; turned to face it.
    /// Within 500 of an enemy it opens its jaw now and then and lets out a shadowflame, a half again
    /// as fast as the game's (by request, "all boost shoot speed"). With nothing to fight it keeps
    /// to a point over its owner's shoulder, behind them (mine). Drawn as the game draws it, in its
    /// own light, whatever the light where it is.
    /// </summary>
    public class BoneSquadSkull : BoneSquadMinion
    {
        /// <summary>Its shadowflame a half again as fast as the game's (by request).</summary>
        public const float Faster = 1.5f;

        /// <summary>Mine: where it keeps with nothing to fight, from its owner's middle: behind them and over them.</summary>
        private const float Back = 40f, Up = 60f;

        /// <summary>How near an enemy it opens its jaw (aiStyle 10's).</summary>
        private const float Reach = 500f;

        /// <summary>aiStyle 10's ai[0]: how it weaves when close.</summary>
        private float weave;

        /// <summary>aiStyle 10's ai[1]: its clock to the next rush.</summary>
        private float rush;

        /// <summary>The Giant Cursed Skull's ai[2] and ai[3]: its clock to the next shot, and its jaw open.</summary>
        private float shot, open;

        private double frameCounter;

        private int frame;

        public override int Kind => NPCID.GiantCursedSkull;

        public override int Width => 44;

        public override int Height => 44;

        public override string Texture => "Terraria/Images/NPC_" + NPCID.GiantCursedSkull;

        protected override void Fetch(Player owner)
        {
            Projectile.Center = owner.Center;
            Projectile.velocity = Vector2.Zero;
            target = -1;
            shot = open = 0f;
            Projectile.netUpdate = true;
        }

        protected override void SendState(BinaryWriter writer)
        {
            writer.Write(weave);
            writer.Write(rush);
            writer.Write(shot);
            writer.Write(open);
        }

        protected override void ReceiveState(BinaryReader reader)
        {
            weave = reader.ReadSingle();
            rush = reader.ReadSingle();
            shot = reader.ReadSingle();
            open = reader.ReadSingle();
        }

        /// <summary>Where it goes, as aiStyle 10 works out its player's middle: the enemy's, or its place by its owner.</summary>
        private Vector2 Aim(Player owner)
        {
            if (target >= 0)
            {
                NPC npc = Main.npc[target];
                return new Vector2(npc.position.X + (float)(npc.width / 2), npc.position.Y + (float)(npc.height / 2));
            }

            int facing = owner.direction == 0 ? 1 : owner.direction;
            return new Vector2(owner.Center.X - facing * Back, owner.Center.Y - Up);
        }

        /// <summary>A tick of the game's Giant Cursed Skull: its AI, then NPC.UpdateNPC's move for one that flies through blocks, then its frame.</summary>
        protected override void Tick(Player owner)
        {
            // NPC.TargetClosest.
            Forget();
            target = Choose(owner);

            float speed = 1f;
            float speedUp = 0.011f;
            Vector2 middle = new Vector2(Projectile.position.X + (float)Projectile.width * 0.5f, Projectile.position.Y + (float)Projectile.height * 0.5f);
            Vector2 aim = Aim(owner);
            float x = aim.X - middle.X;
            float y = aim.Y - middle.Y;
            float length = (float)Math.Sqrt(x * x + y * y);
            float distance = length;

            rush++;
            if (rush > 600f)
            {
                speedUp *= 8f;
                speed = 4f;
                if (rush > 650f)
                    rush = 0f;
            }
            else if (distance < 250f)
            {
                weave += 0.9f;
                if (weave > 0f)
                    Projectile.velocity.Y += 0.019f;
                else
                    Projectile.velocity.Y -= 0.019f;

                if (weave < -100f || weave > 100f)
                    Projectile.velocity.X += 0.019f;
                else
                    Projectile.velocity.X -= 0.019f;

                if (weave > 200f)
                    weave = -200f;
            }

            if (distance > 350f)
            {
                speed = 5f;
                speedUp = 0.3f;
            }
            else if (distance > 300f)
            {
                speed = 3f;
                speedUp = 0.2f;
            }
            else if (distance > 250f)
            {
                speed = 1.5f;
                speedUp = 0.1f;
            }

            length = speed / length;
            x *= length;
            y *= length;

            if (Projectile.velocity.X < x)
                Projectile.velocity.X += speedUp;
            else if (Projectile.velocity.X > x)
                Projectile.velocity.X -= speedUp;

            if (Projectile.velocity.Y < y)
                Projectile.velocity.Y += speedUp;
            else if (Projectile.velocity.Y > y)
                Projectile.velocity.Y -= speedUp;

            if (x > 0f)
            {
                Projectile.spriteDirection = -1;
                Projectile.rotation = (float)Math.Atan2(y, x);
            }

            if (x < 0f)
            {
                Projectile.spriteDirection = 1;
                Projectile.rotation = (float)Math.Atan2(y, x) + 3.14f;
            }

            Shoot();

            // NPC.UpdateNPC, for one that flies through blocks.
            if ((double)Projectile.velocity.X < 0.005 && (double)Projectile.velocity.X > -0.005)
                Projectile.velocity.X = 0f;

            Projectile.position += Projectile.velocity;

            Frame();
        }

        /// <summary>The Giant Cursed Skull's shot: near an enemy, two seconds shut, then its jaw open for 40 ticks and a shadowflame at 20; away from one, it starts over.</summary>
        private void Shoot()
        {
            if (!Fighting)
            {
                shot = open = 0f;
                return;
            }

            NPC enemy = Main.npc[target];
            Vector2 middle = new Vector2(Projectile.position.X + (float)Projectile.width * 0.5f, Projectile.position.Y + (float)Projectile.height * 0.5f);
            float x = enemy.position.X + (float)(enemy.width / 2) - middle.X;
            float y = enemy.position.Y + (float)(enemy.height / 2) - middle.Y;
            float length = (float)Math.Sqrt(x * x + y * y);

            if (length > Reach)
            {
                shot = open = 0f;
                return;
            }

            shot++;
            if (open == 0f)
            {
                if (shot > 120f)
                {
                    shot = 0f;
                    open = 1f;
                    Projectile.netUpdate = true;
                }

                return;
            }

            if (shot > 40f)
                open = 0f;

            if (shot == 20f && Projectile.owner == Main.myPlayer)
            {
                length = 6f * Faster / length;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), middle, new Vector2(x * length, y * length), ModContent.ProjectileType<BoneSquadShadowflames>(), ShotDamage(25), 0f, Projectile.owner);
            }
        }

        /// <summary>NPC.FindFrame for the Giant Cursed Skull: three frames round, its jaw open (the next three) while it shoots.</summary>
        private void Frame()
        {
            frameCounter++;
            if (frameCounter < 4.0)
            {
                frame = 0;
            }
            else if (frameCounter < 8.0)
            {
                frame = 1;
            }
            else
            {
                frame = 2;
                frameCounter = 0.0;
            }

            if (open == 1f)
                frame += 3;
        }

        /// <summary>NPC.GetAlpha for the Giant Cursed Skull, whatever the light; Main.DrawNPCDirect draws its frames two short.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            DrawNpc(Kind, frame, Projectile.rotation, new Color(250, 250, 250, 50), 0f, 2);
            return false;
        }
    }
}
