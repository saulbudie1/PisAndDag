using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles.Lab
{
    /// <summary>
    /// The Abandoned Lab's elevator, from the way in down to the document room, by request ("Ride a car"; and Calamity
    /// Fables looked at for how its own works: a car that runs on a rail between floors, called and sent at panels, that
    /// carries whoever stands on it). A cage car five tiles wide, its floor a plate that whoever stands on is carried by;
    /// two floors (LabPlan.ElevatorTop and ElevatorBottom); a panel at each (LabElevatorPanel): pressed, it calls the car
    /// to that floor, or, the car there, sends it to the other. It eases off as it goes, a clunk when it stops; its cable
    /// up the shaft over it.
    ///
    /// It is a thing of the place, not a creature: nothing hits it, it hits nothing, it is not in the bestiary, no name
    /// shows over it, it never goes away. One only; the lab keeps it (LabElevatorSystem).
    /// </summary>
    public class LabElevator : ModNPC
    {
        public const int Top = 0, Bottom = 1;

        /// <summary>Its top speed (pixels a tick), and its plate.</summary>
        private const float Speed = 4f;
        private const int PlateHigh = 8;

        /// <summary>Where the plate's top is, at each floor.</summary>
        public static float FloorY(int floor) => (floor == Top ? LabPlan.ElevatorTop : LabPlan.ElevatorBottom) * 16f;

        private int Floor
        {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }

        private bool moving;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.NPCBestiaryDrawModifiers hidden = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hidden);
        }

        public override void SetDefaults()
        {
            NPC.width = LabPlan.ElevatorWide * 16;
            NPC.height = PlateHigh;
            NPC.lifeMax = 250;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.knockBackResist = 0f;
            NPC.friendly = true;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.value = 0f;
            NPC.ShowNameOnHover = false;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
        }

        public override bool CheckActive() => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool? CanBeHitByItem(Player player, Item item) => false;

        public override bool? CanBeHitByProjectile(Projectile projectile) => false;

        public override bool CanChat() => false;

        /// <summary>Pressed at a panel: the car called to that floor, or, there already and still, sent to the other.</summary>
        public static void Press(int floor)
        {
            NPC car = LabElevatorSystem.Car;

            if (car == null)
                return;

            var lift = (LabElevator)car.ModNPC;
            bool here = lift.Floor == floor && !lift.moving && Math.Abs(car.position.Y - FloorY(floor)) < 1f;

            lift.Floor = here ? 1 - floor : floor;
            car.netUpdate = true;
            SoundEngine.PlaySound(SoundID.MenuTick, car.Center);
            SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.4f, Pitch = -0.6f }, car.Center);
        }

        public override void AI()
        {
            NPC.timeLeft = 600;
            NPC.position.X = LabPlan.ElevatorLeft * 16f;
            NPC.velocity.X = 0f;

            float target = FloorY(Floor);
            float off = target - NPC.position.Y;

            if (Math.Abs(off) > 0.5f)
            {
                // Easing in and out: slower near either end of the way.
                float fromStart = Math.Abs(NPC.position.Y - FloorY(1 - Floor));
                float speed = Math.Clamp(Math.Min(Math.Abs(off), fromStart + 8f) * 0.06f, 0.8f, Speed);
                NPC.velocity.Y = Math.Sign(off) * Math.Min(speed, Math.Abs(off));
                moving = true;
            }
            else
            {
                NPC.velocity.Y = 0f;
                NPC.position.Y = target;

                if (moving)
                {
                    moving = false;
                    SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.7f, Pitch = -0.5f }, NPC.Center);
                }
            }

            Carry();
            Lighting.AddLight(NPC.Center + new Vector2(0f, -76f), 0.35f, 0.24f, 0.1f);
        }

        /// <summary>Whoever stands on its plate (not jumping off it) is carried with it: put on it where it will be this tick.</summary>
        private void Carry()
        {
            float plate = NPC.position.Y + NPC.velocity.Y;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead || player.velocity.Y < Math.Min(0f, NPC.velocity.Y) - 0.5f)
                    continue;

                float feet = player.position.Y + player.height;

                if (feet < plate - 10f || feet > plate + 14f)
                    continue;

                if (player.position.X + player.width < NPC.position.X + 4f || player.position.X > NPC.position.X + NPC.width - 4f)
                    continue;

                player.position.Y = plate - player.height;
                player.velocity.Y = 0f;
                player.fallStart = (int)(player.position.Y / 16f);
                player.gfxOffY = 0f;
            }
        }

        /// <summary>The cage over its plate, lit as the place is; its cable up the shaft.</summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D cage = TextureAssets.Npc[Type].Value;
            Vector2 bottomLeft = new Vector2((int)NPC.position.X, (int)(NPC.position.Y + PlateHigh)) - screenPos;
            Vector2 corner = bottomLeft - new Vector2(0f, cage.Height);
            Color lit = Lighting.GetColor((NPC.Center - new Vector2(0f, 40f)).ToTileCoordinates());

            int roof = (int)corner.Y;
            int top = (int)((LabPlan.ElevatorTop - 10) * 16f - screenPos.Y);

            if (roof > top)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                int cx = (int)(corner.X + cage.Width * 0.5f) / 2 * 2;
                spriteBatch.Draw(pixel, new Rectangle(cx - 2, top, 2, roof - top), new Color(110, 116, 126).MultiplyRGB(lit));
                spriteBatch.Draw(pixel, new Rectangle(cx, top, 2, roof - top), new Color(64, 70, 80).MultiplyRGB(lit));
            }

            spriteBatch.Draw(cage, new Vector2((int)corner.X / 2 * 2, (int)corner.Y / 2 * 2), lit);
            return false;
        }
    }

    /// <summary>Keeps the lab's one elevator car: put at the top floor when the lab is come into, and again if it is ever gone.</summary>
    public class LabElevatorSystem : ModSystem
    {
        private static int car = -1;

        public static NPC Car => car >= 0 && car < Main.maxNPCs && Main.npc[car].active && Main.npc[car].type == ModContent.NPCType<LabElevator>() ? Main.npc[car] : null;

        public override void ClearWorld() => car = -1;

        public override void PostUpdateNPCs()
        {
            if (!LabFireSystem.InLab || Main.netMode == NetmodeID.MultiplayerClient || Car != null)
                return;

            int type = ModContent.NPCType<LabElevator>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == type)
                {
                    car = npc.whoAmI;
                    return;
                }
            }

            int x = LabPlan.ElevatorLeft * 16 + LabPlan.ElevatorWide * 8;
            int y = (int)LabElevator.FloorY(LabElevator.Top) + 8;
            car = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), x, y, type);
        }
    }
}
