using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.UI;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// Drawing her: on Triunfo (him first, under her), or on her own feet, or in the air, or
    /// on the ground; her bark over her head; the little pool under her.
    ///
    /// Riding, back to front: Triunfo, her body and near leg, the lance, the arm whose fist
    /// is round the lance's grip, her face, her hair. On foot the hand hangs under her face
    /// and hair; lying, it is reached out in front of them and drawn over them, by request.
    ///
    /// The hair is its own layer so that it can move (by request): it is drawn in bands two
    /// rows high, the top band still and each one under it pushed a little further sideways,
    /// in whole pixels. A slow wave goes down it always; the wind blows it its way and makes
    /// it flutter with how hard it blows; and when she is going, the ends trail.
    ///
    /// The lance is only drawn in her hand while it is in her hand: thrown, it is its own
    /// (DonaLanceThrown). In close for the swing and through it, she and Triunfo and the lance
    /// carry the crowd-control tell's yellow outline (CCBypass), drawn under all of them.
    /// </summary>
    public partial class DonaQuinn : ICCBypassOutline
    {
        private static Asset<Texture2D> ridingBody;
        private static Asset<Texture2D> ridingHand;
        private static Asset<Texture2D> ridingHead;
        private static Asset<Texture2D> ridingHair;
        private static Asset<Texture2D> idleBody;
        private static Asset<Texture2D> idleHand;
        private static Asset<Texture2D> idleFace;
        private static Asset<Texture2D> idleHair;
        private static Asset<Texture2D> lance;

        /// <summary>The pool: its colour at the middle and at the rim.</summary>
        private static readonly Color PoolDeep = new Color(96, 8, 14);
        private static readonly Color PoolRim = new Color(64, 6, 10);

        public override void Load()
        {
            if (Main.dedServ)
                return;

            ridingBody = ModContent.Request<Texture2D>(DonaRig.Folder + "DonaQuinnRiding_Body");
            ridingHand = ModContent.Request<Texture2D>(DonaRig.Folder + "DonaQuinnRiding_Hand");
            ridingHead = ModContent.Request<Texture2D>(DonaRig.Folder + "DonaQuinnRiding_Head");
            ridingHair = ModContent.Request<Texture2D>(DonaRig.Folder + "DonaQuinnRiding_HairBob");
            idleBody = ModContent.Request<Texture2D>(DonaRig.Folder + "Idle_Torso_AndLeg");
            idleHand = ModContent.Request<Texture2D>(DonaRig.Folder + "Idle_Hand_Toppng");
            idleFace = ModContent.Request<Texture2D>(DonaRig.Folder + "Idle_Face");
            idleHair = ModContent.Request<Texture2D>(DonaRig.Folder + "Idle_Hair");
            lance = ModContent.Request<Texture2D>(DonaRig.Folder + "Lance_AlsoGore");
        }

        public override void Unload()
        {
            ridingBody = null;
            ridingHand = null;
            ridingHead = null;
            ridingHair = null;
            idleBody = null;
            idleHand = null;
            idleFace = null;
            idleHair = null;
            lance = null;
        }

        /// <summary>Every tick, every machine: how she holds herself, her voice's wait, and from the fall the blood and the shakes.</summary>
        private void Visuals(TriunfoTheDonkey donkey)
        {
            if (barkWait > 0)
                barkWait--;

            if (Mounted && donkey != null)
                RidingVisuals(donkey);
            else
                Aftermath();
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Color light = NPC.GetAlpha(drawColor);
            TriunfoTheDonkey donkey = Donkey();

            if (InSaddle && donkey != null)
            {
                // Him under her, unless the news has reached him first that she is off and he
                // is drawing himself (a client, for a tick or two).
                if (donkey.Carrying)
                {
                    Color donkeyLight = donkey.NPC.GetAlpha(Lighting.GetColor(donkey.NPC.Center.ToTileCoordinates()));
                    donkey.DrawBody(spriteBatch, screenPos, donkeyLight);
                }

                DrawRiding(spriteBatch, screenPos, light, RidingPose(donkey), donkey.NPC.velocity.X);
                return false;
            }

            DrawPool(spriteBatch, screenPos);
            DrawOnFoot(spriteBatch, screenPos, light);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            TriunfoTheDonkey donkey = Donkey();

            DonaRig.Pose pose = InSaddle && donkey != null
                ? RidingPose(donkey)
                : DonaRig.OnFoot(VisualMiddle(), facing, body, armRel, headRel);

            Bark.Draw(spriteBatch, NPC, BarkPoint(pose), screenPos);
        }

        private void DrawRiding(SpriteBatch spriteBatch, Vector2 screenPos, Color light, DonaRig.Pose pose, float speedX)
        {
            int d = pose.Facing;

            DonaRig.Piece(spriteBatch, ridingBody.Value, null, pose.Body, DonaRig.RidingSeat, DonaRig.CanvasWidth,
                pose.BodyTurn, d, light, screenPos);

            if (hasLance)
            {
                DonaRig.Piece(spriteBatch, lance.Value, null, pose.FistAt, DonaRig.Grip, DonaRig.LanceWidth,
                    pose.LanceTurn, d, light, screenPos);
            }

            DonaRig.Piece(spriteBatch, ridingHand.Value, null, pose.ShoulderAt, DonaRig.RidingShoulder, DonaRig.CanvasWidth,
                pose.ArmTurn, d, light, screenPos);

            DonaRig.Piece(spriteBatch, ridingHead.Value, null, pose.NapeAt, DonaRig.RidingNape, DonaRig.CanvasWidth,
                pose.HeadTurn, d, light, screenPos);

            DrawHair(spriteBatch, ridingHair.Value, DonaRig.RidingHairTop, DonaRig.RidingHairBottom,
                pose.NapeAt + new Vector2(0f, hairLag), DonaRig.RidingNape, pose.HeadTurn, d, light, screenPos, speedX);
        }

        /// <summary>
        /// The crowd-control tell's outline (CCBypass), under everything that is drawn of the two
        /// of them in the saddle: Triunfo, her, the lance if she has it. Called by PADGlobalNPC in
        /// place of the outline of her registered picture, which is not what is drawn.
        /// </summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            TriunfoTheDonkey donkey = Donkey();

            if (!InSaddle || donkey == null || Main.dedServ)
                return;

            DonaRig.Pose pose = RidingPose(donkey);
            int d = pose.Facing;

            if (donkey.Carrying)
            {
                DonaRig.Outline(spriteBatch, Terraria.GameContent.TextureAssets.Npc[donkey.Type].Value, DonaRig.Frame(donkey.Picture),
                    donkey.HoovesAt, DonaRig.Hooves, DonaRig.SheetWidth, 0f, donkey.Facing, screenPos);
            }

            DonaRig.Outline(spriteBatch, ridingBody.Value, null, pose.Body, DonaRig.RidingSeat, DonaRig.CanvasWidth, pose.BodyTurn, d, screenPos);

            if (hasLance)
                DonaRig.Outline(spriteBatch, lance.Value, null, pose.FistAt, DonaRig.Grip, DonaRig.LanceWidth, pose.LanceTurn, d, screenPos);

            DonaRig.Outline(spriteBatch, ridingHand.Value, null, pose.ShoulderAt, DonaRig.RidingShoulder, DonaRig.CanvasWidth, pose.ArmTurn, d, screenPos);
            DonaRig.Outline(spriteBatch, ridingHead.Value, null, pose.NapeAt, DonaRig.RidingNape, DonaRig.CanvasWidth, pose.HeadTurn, d, screenPos);
            DonaRig.Outline(spriteBatch, ridingHair.Value, null, pose.NapeAt + new Vector2(0f, hairLag), DonaRig.RidingNape, DonaRig.CanvasWidth,
                pose.HeadTurn, d, screenPos);
        }

        /// <summary>
        /// A hair layer, band by band from <paramref name="top"/> to <paramref name="bottom"/>,
        /// each band pushed sideways by its sway (<see cref="HairShift"/>).
        /// </summary>
        private void DrawHair(SpriteBatch spriteBatch, Texture2D hair, int top, int bottom, Vector2 at, Vector2 pivot,
            float turn, int facing, Color light, Vector2 screenPos, float speedX)
        {
            float span = bottom - top;

            for (int row = top; row < bottom; row += DonaRig.HairBand)
            {
                float depth = row - top + DonaRig.HairBand * 0.5f;

                DonaRig.Band(spriteBatch, hair, row, DonaRig.HairBand, at, pivot, DonaRig.CanvasWidth, turn, facing, light,
                    screenPos, HairShift(depth, span, speedX));
            }
        }

        /// <summary>
        /// How far sideways the band <paramref name="depth"/> rows down a hair <paramref name="span"/>
        /// rows long is pushed this frame, in whole pixels, world right positive. The top
        /// does not move and the ends move most. Always a little, a slow wave down it; the
        /// wind, out of doors, blows it its way and makes it flutter with how hard it blows;
        /// and going along, the ends trail behind.
        /// </summary>
        private float HairShift(float depth, float span, float speedX)
        {
            float reach = MathHelper.Clamp(depth / span, 0f, 1f);
            reach *= reach;

            float t = Main.GlobalTimeWrappedHourly;
            float phase = NPC.whoAmI * 0.7f;

            float wind = NPC.Center.Y < Main.worldSurface * 16.0 ? MathHelper.Clamp(Main.WindForVisuals, -1f, 1f) : 0f;

            float breathing = 1.0f * (float)Math.Sin(t * 2.2f + phase - depth * 0.18f);
            float blown = wind * 1.8f + Math.Abs(wind) * 0.8f * (float)Math.Sin(t * 7.3f + phase - depth * 0.35f);
            float trailing = -MathHelper.Clamp(speedX * 0.28f, -2f, 2f);

            return (float)Math.Round((breathing + blown + trailing) * reach);
        }

        private void DrawOnFoot(SpriteBatch spriteBatch, Vector2 screenPos, Color light)
        {
            Vector2 middle = VisualMiddle() + ShakeOffset();
            middle = new Vector2((float)Math.Round(middle.X), (float)Math.Round(middle.Y));

            DonaRig.Pose pose = DonaRig.OnFoot(middle, facing, body + ShakeTurn(), armRel, headRel);
            int d = pose.Facing;

            // Lying (turned more than an eighth), her hand is reached out past her face and
            // drawn over it; on her feet it hangs under her face and hair.
            bool handOver = Math.Cos(body) < 0.7f;

            DonaRig.Piece(spriteBatch, idleBody.Value, null, pose.Body, DonaRig.Middle, DonaRig.CanvasWidth,
                pose.BodyTurn, d, light, screenPos);

            if (!handOver)
                DrawHand(spriteBatch, screenPos, light, pose);

            // Her head always the way she faces: to look behind her, she turns round.
            float headTurn = pose.HeadTurn;

            DonaRig.Piece(spriteBatch, idleFace.Value, null, pose.NapeAt, DonaRig.Nape, DonaRig.CanvasWidth,
                headTurn, d, light, screenPos);

            DrawHair(spriteBatch, idleHair.Value, DonaRig.HairTop, DonaRig.HairBottom, pose.NapeAt, DonaRig.Nape,
                headTurn, d, light, screenPos, NPC.velocity.X);

            if (handOver)
                DrawHand(spriteBatch, screenPos, light, pose);
        }

        private static void DrawHand(SpriteBatch spriteBatch, Vector2 screenPos, Color light, DonaRig.Pose pose)
        {
            DonaRig.Piece(spriteBatch, idleHand.Value, null, pose.ShoulderAt, DonaRig.Shoulder, DonaRig.CanvasWidth,
                pose.ArmTurn, pose.Facing, light, screenPos);
        }

        /// <summary>
        /// The little pool, on the ground where her chest came down: a flat, stepped oval in
        /// two-pixel rows like the rest of the art, darker at the rim, lit as the ground there
        /// is, fading slowly once she is up.
        /// </summary>
        private void DrawPool(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (pool < 2f || poolFade <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color lit = Lighting.GetColor(poolAt.ToTileCoordinates());
            float alpha = 0.9f * poolFade * NPC.Opacity;

            // Seen from the side a pool is a thin lens on the ground: one row, and once it has
            // spread a little, a shorter darker one on top of it.
            int width = (int)(pool / 2f) * 2;
            int rows = width >= 14 ? 2 : 1;

            for (int r = 0; r < rows; r++)
            {
                int rowWidth = r == 0 ? width : Math.Max(2, (width * 6 / 10) / 2 * 2);
                int x = (int)(poolAt.X - rowWidth / 2f - screenPos.X);
                int y = (int)(poolAt.Y - 2 - r * 2 - screenPos.Y);

                Color colour = r == 0 ? PoolDeep : PoolRim;
                spriteBatch.Draw(pixel, new Rectangle(x, y, rowWidth, 2), new Rectangle(0, 0, 1, 1),
                    colour.MultiplyRGB(lit) * alpha);
            }
        }
    }
}
