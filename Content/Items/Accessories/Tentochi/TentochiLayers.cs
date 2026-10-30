using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>The pieces the Tentochi layers draw with: the game's white pixel, a hard line of it, and the mod's soft lights.</summary>
    internal static class TentochiDraw
    {
        public static Rectangle Pixel => new Rectangle(0, 0, 1, 1);

        /// <summary>A hard line of the white pixel, this wide, between two world points, into the player's draw list.</summary>
        public static void Line(ref PlayerDrawSet drawInfo, Vector2 from, Vector2 to, float width, Color colour)
        {
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f)
                return;

            drawInfo.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value, from - Main.screenPosition, Pixel, colour, way.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0));
        }

        /// <summary>A soft line of light (the mod's SoftLine), this wide, between two world points.</summary>
        public static void Soft(ref PlayerDrawSet drawInfo, Vector2 from, Vector2 to, float width, Color colour)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            Texture2D line = EmpressArt.Line.Value;
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f)
                return;

            drawInfo.DrawDataCache.Add(new DrawData(line, from - Main.screenPosition, null, colour, way.ToRotation(), new Vector2(0f, line.Height * 0.5f),
                new Vector2(length / line.Width, width / line.Height), SpriteEffects.None, 0));
        }

        /// <summary>A soft ball of light (the mod's GlowSoft), this many world pixels across its bright middle.</summary>
        public static void Bloom(ref PlayerDrawSet drawInfo, Vector2 at, float across, Color colour)
        {
            if (!EmpressArt.Ready(EmpressArt.Soft))
                return;

            Texture2D soft = EmpressArt.Soft.Value;
            drawInfo.DrawDataCache.Add(new DrawData(soft, at - Main.screenPosition, null, colour, 0f, soft.Size() * 0.5f, across / (soft.Width * 0.5f), SpriteEffects.None, 0));
        }

        /// <summary>A square of light on the world's 2x2 grid.</summary>
        public static void Dot(ref PlayerDrawSet drawInfo, Vector2 at, float side, Color colour)
        {
            Vector2 corner = new Vector2((float)Math.Floor(at.X / 2f) * 2f, (float)Math.Floor(at.Y / 2f) * 2f) - new Vector2(side * 0.5f);
            drawInfo.DrawDataCache.Add(new DrawData(TextureAssets.MagicPixel.Value, corner - Main.screenPosition, Pixel, colour, 0f, Vector2.Zero, side, SpriteEffects.None, 0));
        }

        /// <summary>The four corners of a box drawn round a world rectangle: a lock's brackets.</summary>
        public static void Brackets(ref PlayerDrawSet drawInfo, Rectangle box, float arm, float width, Color colour)
        {
            Vector2[] corners = { new(box.Left, box.Top), new(box.Right, box.Top), new(box.Right, box.Bottom), new(box.Left, box.Bottom) };
            Vector2[] across = { new(1, 0), new(-1, 0), new(-1, 0), new(1, 0) };
            Vector2[] down = { new(0, 1), new(0, 1), new(0, -1), new(0, -1) };

            for (int i = 0; i < 4; i++)
            {
                Line(ref drawInfo, corners[i], corners[i] + across[i] * arm, width, colour);
                Line(ref drawInfo, corners[i], corners[i] + down[i] * arm, width, colour);
            }
        }

        /// <summary>Where a point in a body frame (40 by 56, the game's) is in the world, as the game lays back accessories over the body.</summary>
        public static Vector2 OnBody(ref PlayerDrawSet drawInfo, Vector2 framePoint)
        {
            Player p = drawInfo.drawPlayer;
            Vector2 middle = drawInfo.Position + p.bodyPosition + new Vector2(p.width / 2, p.height - p.bodyFrame.Height / 2) + new Vector2(0f, 4f);
            Vector2 local = framePoint - drawInfo.bodyVect;

            if (drawInfo.playerEffect.HasFlag(SpriteEffects.FlipHorizontally))
                local.X = -local.X;

            if (drawInfo.playerEffect.HasFlag(SpriteEffects.FlipVertically))
                local.Y = -local.Y;

            return middle.Floor() + local.RotatedBy(p.bodyRotation);
        }
    }

    /// <summary>
    /// Enterprise's Complimentary (by request: "a thin 2 pixel wide ray that is pointed at the cursor from the center of the
    /// player, allowing for more accurate shots when holding ranged weapons"): while a ranged weapon is held, a laser from the
    /// wearer's middle through the cursor, two of the world's pixels wide, stopping at the first solid block or the first
    /// enemy it meets (a lock's brackets round that one), a soft red about it and a pulse running out along it now and then;
    /// a point of light where it ends, a small turning reticle round it. Drawn on the wearer's own machine only: the cursor
    /// is theirs.
    /// </summary>
    public class TentochiSight : PlayerDrawLayer
    {
        /// <summary>How far it reaches, at most: a hundred tiles.</summary>
        public const float Range = 1600f;

        public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.HeldItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;

            if (drawInfo.shadow != 0f || Main.gameMenu || p.whoAmI != Main.myPlayer || p.dead || MimicSystem.Is(p))
                return false;

            if (!p.GetModPlayer<TentochiPlayer>().Sight)
                return false;

            Item held = p.HeldItem;
            return !held.IsAir && held.damage > 0 && held.CountsAsClass(DamageClass.Ranged);
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            Vector2 from = p.MountedCenter + new Vector2(0f, p.gfxOffY);
            Vector2 way = (Main.MouseWorld - from).SafeNormalize(new Vector2(p.direction, 0f));
            float reach = Cast(from, way, out NPC met);
            Vector2 end = from + way * reach;

            // A breath of red about it; the line itself, hard, two pixels; a pulse running out along it.
            TentochiDraw.Soft(ref drawInfo, from, end, 7f, TentochiFX.Glow(TentochiFX.Red, 0.32f));
            TentochiDraw.Line(ref drawInfo, from, end, 2f, TentochiFX.Glow(TentochiFX.Red, 0.95f));

            float run = Main.GlobalTimeWrappedHourly * 60f % 70f / 70f;
            float pulse = run * (reach + 40f);

            if (pulse < reach)
            {
                Vector2 a = from + way * Math.Max(0f, pulse - 30f);
                Vector2 b = from + way * pulse;
                TentochiDraw.Line(ref drawInfo, a, b, 2f, TentochiFX.Glow(TentochiFX.Hot, 0.6f));
            }

            // Where it ends: a point, a glow, a small reticle turning.
            TentochiDraw.Bloom(ref drawInfo, end, 16f, TentochiFX.Glow(TentochiFX.Red, 0.75f));
            TentochiDraw.Dot(ref drawInfo, end, 4f, TentochiFX.Glow(TentochiFX.Hot, 1f));

            float turn = Main.GlobalTimeWrappedHourly * 2.4f;

            for (int i = 0; i < 4; i++)
            {
                Vector2 spoke = (turn + i * MathHelper.PiOver2).ToRotationVector2();
                TentochiDraw.Line(ref drawInfo, end + spoke * 7f, end + spoke * 11f, 2f, TentochiFX.Glow(TentochiFX.Red, 0.8f));
            }

            if (met != null)
            {
                Rectangle box = met.Hitbox;
                box.Inflate(6, 6);
                TentochiDraw.Brackets(ref drawInfo, box, 8f, 2f, TentochiFX.Glow(TentochiFX.Red, 0.9f));
            }
        }

        /// <summary>
        /// How far along the line the laser goes before something stops it: the first block that is solid (not a platform),
        /// or the first enemy whose hitbox the line crosses before that; at most <see cref="Range"/>.
        /// </summary>
        public static float Cast(Vector2 from, Vector2 way, out NPC met)
        {
            met = null;
            float reach = Range;

            for (float d = 0f; d < Range; d += 4f)
            {
                Vector2 at = from + way * d;
                int i = (int)(at.X / 16f);
                int j = (int)(at.Y / 16f);

                if (!WorldGen.InWorld(i, j, 1))
                {
                    reach = d;
                    break;
                }

                Tile tile = Main.tile[i, j];

                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    reach = d;
                    break;
                }
            }

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];

                if (!npc.CanBeChasedBy())
                    continue;

                float t = Enter(from, way, npc.Hitbox);

                if (t >= 0f && t < reach)
                {
                    reach = t;
                    met = npc;
                }
            }

            return reach;
        }

        /// <summary>How far along a ray it enters a box (the slab method), or -1 if it never does.</summary>
        private static float Enter(Vector2 from, Vector2 way, Rectangle box)
        {
            float near = 0f;
            float far = float.MaxValue;

            for (int axis = 0; axis < 2; axis++)
            {
                float o = axis == 0 ? from.X : from.Y;
                float d = axis == 0 ? way.X : way.Y;
                float min = axis == 0 ? box.Left : box.Top;
                float max = axis == 0 ? box.Right : box.Bottom;

                if (Math.Abs(d) < 1e-5f)
                {
                    if (o < min || o > max)
                        return -1f;

                    continue;
                }

                float t1 = (min - o) / d;
                float t2 = (max - o) / d;

                if (t1 > t2)
                    (t1, t2) = (t2, t1);

                near = Math.Max(near, t1);
                far = Math.Min(far, t2);

                if (near > far)
                    return -1f;
            }

            return near;
        }
    }

    /// <summary>
    /// The Back Rocket's launcher, on the wearer's back (its own picture, two frames: shut, and its tubes kicked back as a
    /// rocket leaves), behind the body as the game lays back accessories, its sensor's light in their red: blinking slowly
    /// while nothing is held, burning steady when something is, with a lock's brackets closing on that one on the wearer's
    /// own machine; a flash at the tubes as a rocket leaves.
    /// </summary>
    public class TentochiLauncher : PlayerDrawLayer
    {
        private static Asset<Texture2D> picture;

        /// <summary>
        /// Where the launcher's middle sits in the body frame (the game's 40 by 56), facing right: against the upper back,
        /// its tubes up past the shoulder. Its sensor and its left tube's mouth, from its middle (BackRocketLauncher.png,
        /// 22 by 22 a frame).
        /// </summary>
        public static readonly Vector2 Seat = new Vector2(8f, 30f);
        public static readonly Vector2 Sensor = new Vector2(-6f, 4f);
        public static readonly Vector2 Tubes = new Vector2(-5f, -8f);

        public override void Load()
        {
            if (!Main.dedServ)
                picture = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/Tentochi/BackRocketLauncher");
        }

        public override void Unload() => picture = null;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.BackAcc);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            TentochiPlayer t = p.GetModPlayer<TentochiPlayer>();
            return !p.dead && t.Launcher != null && t.LauncherShown && !drawInfo.hideEntirePlayer;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (picture == null || !picture.IsLoaded)
                return;

            Player p = drawInfo.drawPlayer;
            TentochiPlayer t = p.GetModPlayer<TentochiPlayer>();
            Texture2D texture = picture.Value;
            Rectangle frame = texture.Frame(1, 2, 0, t.SinceShot < 8 ? 1 : 0);

            // The body's bob in its walk is the head's (Main.OffsetsPlayerHeadgear, one for each body frame).
            int bodyIndex = p.bodyFrame.Height > 0 ? p.bodyFrame.Y / p.bodyFrame.Height : 0;
            Vector2 bob = bodyIndex >= 0 && bodyIndex < Main.OffsetsPlayerHeadgear.Length ? Main.OffsetsPlayerHeadgear[bodyIndex] - new Vector2(0f, 2f) : Vector2.Zero;
            Vector2 kick = t.SinceShot < 8 ? new Vector2(0f, 2f * (1f - t.SinceShot / 8f)) : Vector2.Zero;
            Vector2 middle = TentochiDraw.OnBody(ref drawInfo, Seat + bob + kick);

            Color light = drawInfo.colorArmorBody;
            drawInfo.DrawDataCache.Add(new DrawData(texture, middle.Floor() - Main.screenPosition, frame, light, p.bodyRotation, frame.Size() * 0.5f, 1f, drawInfo.playerEffect, 0));

            if (drawInfo.shadow != 0f)
                return;

            Vector2 sensor = TentochiDraw.OnBody(ref drawInfo, Seat + bob + kick + Sensor);
            bool holding = t.Target >= 0;
            float blink = holding ? 1f : (Main.GlobalTimeWrappedHourly * 2f % 1f < 0.5f ? 0.9f : 0.25f);
            TentochiDraw.Dot(ref drawInfo, sensor, 2f, TentochiFX.Glow(TentochiFX.Hot, blink));
            TentochiDraw.Bloom(ref drawInfo, sensor, holding ? 6f : 4f, TentochiFX.Glow(TentochiFX.Red, 0.6f * blink));

            if (t.SinceShot < 6)
            {
                Vector2 tubes = TentochiDraw.OnBody(ref drawInfo, Seat + bob + Tubes);
                TentochiDraw.Bloom(ref drawInfo, tubes, 22f * (1f - t.SinceShot / 6f), TentochiFX.Glow(TentochiFX.Amber, 0.8f));
            }

            if (holding && p.whoAmI == Main.myPlayer && t.Target < Main.maxNPCs && Main.npc[t.Target].active)
            {
                // The lock: brackets closing in on what it holds over its first third of a second, then holding, pulsing.
                NPC npc = Main.npc[t.Target];
                float close = MathHelper.Clamp(t.Held / 20f, 0f, 1f);
                Rectangle box = npc.Hitbox;
                int spread = (int)MathHelper.Lerp(26f, 6f, close * close * (3f - 2f * close));
                box.Inflate(spread, spread);
                float pulse = close >= 1f ? 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f) : 0.9f;
                TentochiDraw.Brackets(ref drawInfo, box, 10f, 2f, TentochiFX.Glow(TentochiFX.Red, pulse));

                if (close >= 1f)
                {
                    Vector2 top = new Vector2(npc.Center.X, box.Top - 6f);
                    TentochiDraw.Line(ref drawInfo, top - new Vector2(6f, 0f), top + new Vector2(6f, 0f), 2f, TentochiFX.Glow(TentochiFX.Hot, pulse));
                }
            }
        }
    }

    /// <summary>
    /// The exoskeletons' lights (Exo Air I and II): their glow pictures laid over their legs as the game lays shoes, lit
    /// whatever the light about them, burning while a dash is ready and dim while it recharges, and flaring at the heels
    /// while one goes.
    /// </summary>
    public class TentochiExoGlow : PlayerDrawLayer
    {
        private static Asset<Texture2D> first;
        private static Asset<Texture2D> second;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            first = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/Tentochi/ExoAirI_ShoesGlow");
            second = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/Tentochi/ExoAirII_ShoesGlow");
        }

        public override void Unload() => first = second = null;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Shoes);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            return !p.dead && !drawInfo.isSitting && p.shoe > 0 && (p.shoe == ExoAirI.ShoeSlot || p.shoe == ExoAirII.ShoeSlot);
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            Asset<Texture2D> asset = p.shoe == ExoAirII.ShoeSlot ? second : first;

            if (asset == null || !asset.IsLoaded)
                return;

            AccessoryDash dash = p.GetModPlayer<AccessoryDash>();
            float lit = dash.Dashing ? 1f : dash.Cooldown > 0 ? 0.35f : 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f);
            lit *= 1f - drawInfo.shadow;

            Vector2 at = new Vector2((int)(drawInfo.Position.X - Main.screenPosition.X - p.legFrame.Width / 2 + p.width / 2),
                (int)(drawInfo.Position.Y - Main.screenPosition.Y + p.height - p.legFrame.Height + 4f)) + p.legPosition + drawInfo.legVect;

            drawInfo.DrawDataCache.Add(new DrawData(asset.Value, at, p.legFrame, TentochiFX.Glow(Color.White, lit), p.legRotation, drawInfo.legVect, 1f, drawInfo.playerEffect, 0));

            if (dash.Dashing && dash.Doing != AccessoryDash.Kind.Empress && drawInfo.shadow == 0f)
            {
                // The jets at the heels, burning back.
                Vector2 heels = TentochiFX.Soles(p, -dash.Direction * 7f, 4f) + new Vector2(0f, p.gfxOffY);
                TentochiDraw.Bloom(ref drawInfo, heels, 14f, TentochiFX.Glow(TentochiFX.Red, 0.8f));
                TentochiDraw.Bloom(ref drawInfo, heels, 6f, TentochiFX.Glow(TentochiFX.Hot, 1f));
            }
        }
    }

    /// <summary>
    /// Adam Kit I's core: a light at the wearer's chest in their red, beating slowly as a heart does; fast and unsteady
    /// for a second after it was let go, while it re-arms.
    /// </summary>
    public class TentochiKitCore : PlayerDrawLayer
    {
        /// <summary>The core's place in the body frame, facing right.</summary>
        public static readonly Vector2 Chest = new Vector2(22f, 26f);

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Torso);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            TentochiPlayer t = p.GetModPlayer<TentochiPlayer>();
            return !p.dead && t.Kit && t.KitShown && drawInfo.shadow == 0f && !drawInfo.hideEntirePlayer;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player p = drawInfo.drawPlayer;
            TentochiPlayer t = p.GetModPlayer<TentochiPlayer>();
            int bodyIndex = p.bodyFrame.Height > 0 ? p.bodyFrame.Y / p.bodyFrame.Height : 0;
            Vector2 bob = bodyIndex >= 0 && bodyIndex < Main.OffsetsPlayerHeadgear.Length ? Main.OffsetsPlayerHeadgear[bodyIndex] - new Vector2(0f, 2f) : Vector2.Zero;
            Vector2 core = TentochiDraw.OnBody(ref drawInfo, Chest + bob);

            float time = Main.GlobalTimeWrappedHourly;
            float beat;

            if (t.Rearm > 0)
                beat = Main.rand.NextFloat() < 0.3f ? 0.2f : 0.9f;
            else
            {
                // Two beats a second and a little, the second softer: lub, dub.
                float phase = time * 1.1f % 1f;
                beat = 0.45f + 0.55f * Math.Max(Peak(phase, 0.05f), 0.6f * Peak(phase, 0.25f));
            }

            TentochiDraw.Bloom(ref drawInfo, core, 14f * (0.8f + 0.3f * beat), TentochiFX.Glow(TentochiFX.Red, 0.55f * beat));
            TentochiDraw.Dot(ref drawInfo, core, 2f, TentochiFX.Glow(TentochiFX.Hot, beat));
        }

        private static float Peak(float phase, float at)
        {
            float d = Math.Abs(phase - at) / 0.08f;
            return d >= 1f ? 0f : 1f - d * d;
        }
    }
}
