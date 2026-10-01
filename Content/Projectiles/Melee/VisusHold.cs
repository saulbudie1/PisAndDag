using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// How Visus sits in the hands, worked out the way vanilla works out where a hand is.
    ///
    /// Everything here is in the player's own frame, "upright": facing right, the right way up,
    /// from the player's centre (Player.RotatedRelativePoint of MountedCenter, which is where the
    /// arms are drawn from), x forward and y down. An angle is the spear's, from straight ahead,
    /// down positive. <see cref="ToWorld"/> and <see cref="ToWorldAngle"/> turn it round for a
    /// player facing left and flip it for one under reversed gravity, the way the player's own
    /// sprite is turned and flipped, so a pose is written once.
    ///
    /// The sprite (VisusSpearDiagonal.png) is the spear on the diagonal, the point up and to the
    /// right, in frames of 92 by 92 texels, three down the sheet (the eye open, half shut, shut).
    /// Points on it are given along the shaft from the butt and across it, + toward the hammer
    /// (below the shaft when it is held level, the horn above): the butt at 0, the point at 120.
    /// Where the butt is on the sheet is <see cref="PolearmArt.Butt"/>. The front hand holds it
    /// <c>grip</c> texels from the butt, and the back hand wherever along the shaft the back arm
    /// can reach.
    /// </summary>
    public static class VisusGrip
    {
        public const string SpearTexture = "PisAndDag/Content/Projectiles/Melee/VisusSpearDiagonal";
        public const string GlowTexture = "PisAndDag/Content/Projectiles/Melee/VisusSpearDiagonalGlow";

        /// <summary>Butt to point, in texels.</summary>
        public const float Length = 120f;

        /// <summary>Frames of the eye: open, half shut, shut.</summary>
        public const int Open = 0, Half = 1, Shut = 2;

        /// <summary>The very point, along the sprite from the butt and across from the shaft (+ toward the hammer).</summary>
        public static readonly Vector2 Point = new Vector2(118f, -1f);

        /// <summary>The middle of the hammer's teeth, the edge that leads the blow down.</summary>
        public static readonly Vector2 Teeth = new Vector2(92f, 14f);

        /// <summary>The middle of the eye.</summary>
        public static readonly Vector2 Eye = new Vector2(90f, 0f);

        // ---- the hands (Player.GetFrontHandPosition / GetBackHandPosition, facing right) --------

        private static float FrontReach(Player.CompositeArmStretchAmount stretch) => stretch switch
        {
            Player.CompositeArmStretchAmount.Full => 10f,
            Player.CompositeArmStretchAmount.None => 4f,
            Player.CompositeArmStretchAmount.Quarter => 6f,
            _ => 8f,
        };

        private static Vector2 BackReach(Player.CompositeArmStretchAmount stretch) => stretch switch
        {
            Player.CompositeArmStretchAmount.Full => new Vector2(10f, 12f),
            Player.CompositeArmStretchAmount.None => new Vector2(4f, 6f),
            Player.CompositeArmStretchAmount.Quarter => new Vector2(6f, 8f),
            _ => new Vector2(8f, 10f),
        };

        /// <summary>
        /// The front hand with the front arm turned to <paramref name="angle"/>: vanilla's own
        /// sum, a shoulder at (-4, -2), the arm's length along it and 3 to its side.
        /// </summary>
        public static Vector2 FrontHand(Player.CompositeArmStretchAmount stretch, float angle)
        {
            Vector2 along = angle.ToRotationVector2();
            return new Vector2(-4f, -2f) + along * FrontReach(stretch) + new Vector2(-along.Y, along.X) * 3f;
        }

        /// <summary>The back hand with the back arm turned to <paramref name="angle"/>: a shoulder at (6, -2), an arm a little longer down than across.</summary>
        public static Vector2 BackHand(Player.CompositeArmStretchAmount stretch, float angle)
        {
            Vector2 reach = BackReach(stretch);
            return new Vector2(6f, -2f) + new Vector2((float)Math.Cos(angle) * reach.X, (float)Math.Sin(angle) * reach.Y);
        }

        /// <summary>
        /// The angle that puts the back hand on the shaft: the shaft is the line through the
        /// front hand at the spear's angle. Of the angles that do, the one nearest the spear's
        /// own, so the arm reaches along it; if none reaches, the one that comes closest.
        /// </summary>
        public static float BackArmOnto(Player.CompositeArmStretchAmount stretch, Vector2 hand, float angle)
        {
            Vector2 along = angle.ToRotationVector2();

            float Off(float a)
            {
                Vector2 b = BackHand(stretch, a) - hand;
                return b.X * along.Y - b.Y * along.X;
            }

            const int Steps = 48;

            float best = angle;
            float bestTurn = float.MaxValue;
            float closest = angle;
            float closestOff = float.MaxValue;

            float previous = angle - MathHelper.Pi;
            float previousOff = Off(previous);

            for (int i = 1; i <= Steps; i++)
            {
                float a = angle - MathHelper.Pi + MathHelper.TwoPi * i / Steps;
                float off = Off(a);

                if (Math.Abs(off) < closestOff)
                {
                    closestOff = Math.Abs(off);
                    closest = a;
                }

                if ((previousOff < 0f) != (off < 0f))
                {
                    float lo = previous, hi = a, loOff = previousOff;

                    for (int k = 0; k < 16; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        float midOff = Off(mid);

                        if ((loOff < 0f) != (midOff < 0f))
                            hi = mid;
                        else
                        {
                            lo = mid;
                            loOff = midOff;
                        }
                    }

                    float root = (lo + hi) * 0.5f;
                    float turn = Math.Abs(MathHelper.WrapAngle(root - angle));

                    if (turn < bestTurn)
                    {
                        bestTurn = turn;
                        best = root;
                    }
                }

                previous = a;
                previousOff = off;
            }

            return bestTurn < float.MaxValue ? best : closest;
        }

        // ---- upright to the world ----------------------------------------------------------------

        /// <summary>Where the arms are drawn from.</summary>
        public static Vector2 Centre(Player player) => player.RotatedRelativePoint(player.MountedCenter, true, true);

        public static Vector2 ToWorld(Player player, Vector2 upright) =>
            Centre(player) + new Vector2(upright.X * player.direction, upright.Y * player.gravDir);

        /// <summary>The spear's angle in the world, to draw it at.</summary>
        public static float ToWorldAngle(Player player, float angle)
        {
            float facing = player.direction == 1 ? angle : MathHelper.Pi - angle;
            return player.gravDir == 1f ? facing : -facing;
        }

        /// <summary>
        /// A composite arm's rotation pointing along <paramref name="angle"/>: vanilla's arm hangs
        /// at 0, so it is the angle less a quarter turn; the arm is drawn with the player's own
        /// flips, so under reversed gravity the same pose is the negated one.
        /// </summary>
        public static float ArmRotation(Player player, float angle)
        {
            float facing = player.direction == 1 ? angle : MathHelper.Pi - angle;
            return (facing - MathHelper.PiOver2) * player.gravDir;
        }

        /// <summary>A point on the spear, in the world: <paramref name="along"/> from the butt, <paramref name="across"/> from the shaft toward the hammer.</summary>
        public static Vector2 On(Player player, float angle, Vector2 hand, float grip, float along, float across)
        {
            Vector2 u = angle.ToRotationVector2();
            Vector2 n = new Vector2(-u.Y, u.X);
            return ToWorld(player, hand + u * (along - grip) + n * across);
        }

        /// <summary>
        /// Holds Visus this tick: the front arm along the spear at <paramref name="front"/>, the
        /// back arm onto the shaft at <paramref name="back"/>, and VisusLayer told what to draw.
        /// Returns the front hand, upright. <paramref name="art"/> is which polearm VisusLayer
        /// draws in the hands: Visus unless another is given (the Flesh Maiden, which is Visus
        /// grown over and is held the same way). <paramref name="turned"/> is the polearm turned
        /// over on its shaft, the side below it (the hammer, the blade) now above.
        /// </summary>
        public static Vector2 Hold(Player player, float angle, float grip, Player.CompositeArmStretchAmount front,
            Player.CompositeArmStretchAmount back, int eye, float glow, PolearmArt art = null, bool turned = false)
        {
            Vector2 hand = FrontHand(front, angle);
            float backAngle = BackArmOnto(back, hand, angle);

            player.SetCompositeArmFront(true, front, ArmRotation(player, angle));
            player.SetCompositeArmBack(true, back, ArmRotation(player, backAngle));

            VisusPose pose = player.GetModPlayer<VisusPose>();
            pose.Held = true;
            pose.Angle = angle;
            pose.Grip = grip;
            pose.Hand = hand;
            pose.Eye = eye;
            pose.Glow = glow;
            pose.Art = art ?? PolearmArt.Visus;
            pose.Turned = turned;

            return hand;
        }
    }

    /// <summary>
    /// What VisusLayer draws for a polearm held with VisusGrip: its sheet, the eye's glow, and
    /// how the shaft runs through them. Every sheet is the polearm on the diagonal, the point up
    /// and to the right, in square frames, three down the sheet (the eye open, half shut, shut);
    /// the glow is one frame the same size, the open eye only. The shaft runs from
    /// <see cref="Butt"/> up and to the right at 45 degrees, and the points along and across it
    /// that the code aims with (VisusGrip.Point, Teeth and Eye; FleshMaidenGrip.Point, Eye, Edge
    /// and Hook) are where they are drawn.
    /// </summary>
    public sealed class PolearmArt
    {
        /// <summary>92 by 92 texels a frame (VisusSpearDiagonal.png).</summary>
        public static readonly PolearmArt Visus = new PolearmArt(VisusGrip.SpearTexture, VisusGrip.GlowTexture,
            (int)VisusGrip.Length, 92, new Vector2(5.3604f, 86.6396f));

        /// <summary>Visus grown over: 128 by 128 texels a frame (FleshMaidenSpearDiagonal.png).</summary>
        public static readonly PolearmArt FleshMaiden = new PolearmArt("PisAndDag/Content/Projectiles/Melee/FleshMaidenSpearDiagonal",
            "PisAndDag/Content/Projectiles/Melee/FleshMaidenSpearDiagonalGlow", 168, 128, new Vector2(4.9756f, 121.0244f));

        /// <summary>Every one there is, for VisusLayer to load.</summary>
        internal static readonly PolearmArt[] All = { Visus, FleshMaiden };

        public readonly string SheetPath;
        public readonly string GlowPath;

        /// <summary>Butt to point along the shaft, in texels.</summary>
        public readonly int Length;

        /// <summary>A frame's width and height, in texels.</summary>
        public readonly int FrameSize;

        /// <summary>The butt's end, in texels from the top left of a frame.</summary>
        public readonly Vector2 Butt;

        internal Asset<Texture2D> Sheet;
        internal Asset<Texture2D> Glow;

        private PolearmArt(string sheetPath, string glowPath, int length, int frameSize, Vector2 butt)
        {
            SheetPath = sheetPath;
            GlowPath = glowPath;
            Length = length;
            FrameSize = frameSize;
            Butt = butt;
        }

        /// <summary>The point on the shaft <paramref name="along"/> texels from the butt, in texels of a frame.</summary>
        public Vector2 On(float along) => Butt + new Vector2(along, -along) * 0.70710677f;

        /// <summary>
        /// How to draw frame <paramref name="eye"/> of it (the eye open, half shut, shut), held
        /// <paramref name="grip"/> texels from the butt at <paramref name="rotation"/> in the world,
        /// mirrored across its shaft when <paramref name="flip"/>. Its glow goes on with the same
        /// turn, pivot and mirror. A texture not loaded (on a server, say) comes back null.
        /// </summary>
        public PolearmPlacement Place(int eye, float rotation, float grip, bool flip)
        {
            // The shaft already leans an eighth of a turn up, so it turns an eighth less than it
            // is held. Mirrored top to bottom it leans an eighth down instead; the origin is
            // where it lands on the mirrored frame, so the grip's texel goes with the mirror.
            Vector2 at = On(grip);
            Vector2 pivot = flip ? new Vector2(at.X, FrameSize - at.Y) : at;
            float turn = flip ? rotation - MathHelper.PiOver4 : rotation + MathHelper.PiOver4;
            SpriteEffects effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;

            return new PolearmPlacement(Sheet?.Value, Glow?.Value, new Rectangle(0, FrameSize * eye, FrameSize, FrameSize),
                new Rectangle(0, 0, FrameSize, FrameSize), turn, pivot, effects);
        }
    }

    /// <summary>Where and how a polearm's frame is drawn, as <see cref="PolearmArt.Place"/> works it out.</summary>
    public readonly struct PolearmPlacement
    {
        public readonly Texture2D Texture;
        public readonly Texture2D Glow;
        public readonly Rectangle Frame;
        public readonly Rectangle GlowFrame;
        public readonly float Rotation;
        public readonly Vector2 Origin;
        public readonly SpriteEffects Effects;

        public PolearmPlacement(Texture2D texture, Texture2D glow, Rectangle frame, Rectangle glowFrame, float rotation,
            Vector2 origin, SpriteEffects effects)
        {
            Texture = texture;
            Glow = glow;
            Frame = frame;
            GlowFrame = glowFrame;
            Rotation = rotation;
            Origin = origin;
            Effects = effects;
        }
    }

    /// <summary>
    /// Visus as the player holds it this tick, for VisusLayer to draw. Set by whichever of the
    /// thrust or the blow is out (VisusGrip.Hold, from the projectile's AI on every machine),
    /// cleared at the start of every tick, so the spear is drawn exactly while one of them is.
    ///
    /// Also keeps the blow's rest (<see cref="Rest"/>): the ticks until the blow from overhead
    /// can be struck again, set as a blow ends (VisusChop.OnKill) and read by Visus.CanUseItem.
    /// The eye rests with it (<see cref="RestingEye"/>): shut, half open over the last ticks,
    /// open when the blow is ready.
    /// </summary>
    public class VisusPose : ModPlayer
    {
        public bool Held;
        public float Angle;
        public float Grip;
        public Vector2 Hand;
        public int Eye;
        public float Glow;

        /// <summary>Which polearm is in the hands: Visus, or the Flesh Maiden.</summary>
        public PolearmArt Art;

        /// <summary>Turned over on its shaft: what hangs below it is above.</summary>
        public bool Turned;

        /// <summary>Ticks until the blow from overhead can be struck again.</summary>
        public int Rest;

        /// <summary>The last ticks of the rest, over which the eye is half open.</summary>
        private const int EyeOpens = 10;

        public override void ResetEffects()
        {
            Held = false;
        }

        public override void PostUpdate()
        {
            if (Rest > 0)
                Rest--;
        }

        /// <summary>The eye when nothing is holding it open: shut while the blow rests, open once it is ready.</summary>
        public int RestingEye => Rest > EyeOpens ? VisusGrip.Shut : Rest > 0 ? VisusGrip.Half : VisusGrip.Open;
    }

    /// <summary>
    /// Draws Visus in the player's hands, where vanilla draws a held item behind the front arm:
    /// after the body and the back arm, before the front arm, so the front hand closes over the
    /// shaft and the back hand is under it. Only while VisusPose.Held. The eye's glow goes over
    /// it, added light, while the eye is fully open.
    /// </summary>
    public class VisusLayer : PlayerDrawLayer
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            foreach (PolearmArt art in PolearmArt.All)
            {
                art.Sheet = ModContent.Request<Texture2D>(art.SheetPath);
                art.Glow = ModContent.Request<Texture2D>(art.GlowPath);
            }
        }

        public override void Unload()
        {
            foreach (PolearmArt art in PolearmArt.All)
            {
                art.Sheet = null;
                art.Glow = null;
            }
        }

        // Vanilla's own slot for an item held behind the front arm (ColdCallStaffLayer's too).
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<VisusPose>().Held;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            VisusPose pose = player.GetModPlayer<VisusPose>();
            PolearmArt art = pose.Art ?? PolearmArt.Visus;

            if (art.Sheet == null)
                return;

            bool flip = ((player.direction < 0) != (player.gravDir < 0f)) != pose.Turned;
            float rotation = VisusGrip.ToWorldAngle(player, pose.Angle);
            Vector2 position = (VisusGrip.ToWorld(player, pose.Hand) - Main.screenPosition).Floor();

            // Lit where most of it is: two thirds of the way up.
            Vector2 middle = VisusGrip.On(player, pose.Angle, pose.Hand, pose.Grip, art.Length * 0.66f, 0f);
            Color light = Lighting.GetColor(middle.ToTileCoordinates());

            int eye = Math.Clamp(pose.Eye, VisusGrip.Open, VisusGrip.Shut);
            PolearmPlacement place = art.Place(eye, rotation, pose.Grip, flip);

            if (place.Texture == null)
                return;

            drawInfo.DrawDataCache.Add(new DrawData(place.Texture, position, place.Frame, light, place.Rotation, place.Origin, 1f, place.Effects));

            if (place.Glow != null && eye == VisusGrip.Open && pose.Glow > 0.01f)
            {
                Color added = new Color(255, 255, 255, 0) * MathHelper.Clamp(pose.Glow, 0f, 1f);

                drawInfo.DrawDataCache.Add(new DrawData(place.Glow, position, place.GlowFrame, added, place.Rotation, place.Origin, 1f, place.Effects));
            }
        }
    }
}
