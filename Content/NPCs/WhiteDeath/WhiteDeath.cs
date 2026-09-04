using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace PisAndDag.Content.NPCs.WhiteDeath
{ 
	[AutoloadBossHead]
	public class WhiteDeath : ModNPC
	{ 
		 
		public override void SetDefaults()
		{ 
			NPC.npcSlots = 2f; 
			NPC.width = 32;
			NPC.height = 64;
			NPC.defense = 8;
			NPC.lifeMax = 500; 
			NPC.aiStyle = -1;
			AIType = -1;
			NPC.noGravity = false;
			NPC.noTileCollide = false;
			NPC.boss = true;
			NPC.knockBackResist = 0f;
			NPC.value = (float)Item.buyPrice(0, 10, 0, 0);
			NPC.HitSound = new SoundStyle?(SoundID.NPCHit45);
			NPC.DeathSound = new SoundStyle?(SoundID.NPCDeath1);  
		}
		public int timer = 0;

		// One volley every 45 ticks (0.75s). The old code compared timer against 1/46/91
		// literally, so the boss fired three shots and then idled forever as timer climbed.
		private const int MarksmanInterval = 45;

        public override void AI()
        {
            float lifeRatio = (float)NPC.life / (float)NPC.lifeMax;

            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
            {
                NPC.TargetClosest(true);
            }
            Player player = Main.player[NPC.target];

            // Nobody left to fight — leave rather than hanging around forever.
            if (!player.active || player.dead)
            {
                NPC.velocity.Y -= 0.4f;
                NPC.noTileCollide = true;
                NPC.EncourageDespawn(10);
                return;
            }

            timer += 1;
            if (timer % MarksmanInterval == 1)
			{
				Vector2 randomized_pos = new Vector2(player.position.X + Main.rand.Next(player.width), player.position.Y + Main.rand.Next(player.height));
                int a = Projectile.NewProjectile(base.NPC.GetSource_FromAI(null), randomized_pos, Vector2.Zero, ModContent.ProjectileType<Projectiles.NPCs.Marksman_WhiteDeath>(), 0,0);
                Vector2 offset = randomized_pos - player.Center; 
                Main.projectile[a].ai[0] = offset.X;
                Main.projectile[a].ai[1] = offset.Y;
            }
        }
        public override bool? CanBeHitByItem(Player player, Item item)
        {
			return false;
        }
        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
			return false;
        }
	}
}
