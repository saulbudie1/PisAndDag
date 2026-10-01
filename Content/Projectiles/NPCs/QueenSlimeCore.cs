using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Legends;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.NPCs
{
    /// <summary>
    /// The Queen Slime's core, thrown (Legends; LegendsQueenSlime.Moves): spinning end over end
    /// and red with heat, out through where the player stood, round like a boomerang half a
    /// screen long, and back into her.
    ///
    /// THE WAY. In its own frame, along its axis and out to one side: along = reach sin(pi u),
    /// out = width sin(pi u) u, for u from 0 to 1. Out low along the axis, round the far end
    /// where it slows, and back higher, on the side away from the ground. The axis is turned a
    /// little off the line to the player, so that the way out passes exactly through where they
    /// stood. The last of it bends to wherever her core is by then, and it is gone into her.
    ///
    /// Everything is worked out from its start, its axis and its reach (ai[1], ai[2] with the
    /// side in its sign) and its own clock, so every machine flies it alike; the start and the
    /// clock are sent. The server ends it early if she is no longer waiting for it.
    ///
    /// A hit takes exactly 150 from a player, whatever their armour or the world's difficulty
    /// (ModifyHitPlayer). Its own damage, 75, is what the game would double to that.
    /// </summary>
    public class QueenSlimeCore : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_" + ExtrasID.QueenSlimeCrystalCore;

        public const int Damage = 75;

        /// <summary>What a hit takes from a player, exactly.</summary>
        public const int HitsFor = 150;

        /// <summary>Its reach: half a screen (of 1920), and never less than this far past the player.</summary>
        private const float Reach = 960f;
        private const float Beyond = 160f;
        private const float MostReach = 1600f;

        /// <summary>The loop's width, against its reach.</summary>
        private const float Width = 0.5f;

        /// <summary>How fast it leaves her and comes back, at its fastest: sets how long the whole way takes.</summary>
        private const float PeakSpeed = 13f;

        private const int TrailLength = 10;

        private Vector2 origin;
        private int clock;

        private int Owner => (int)Projectile.ai[0];
        private float Axis => Projectile.ai[1];
        private float SignedReach => Projectile.ai[2];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLength;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.scale = 1.3f;
        }

        /// <summary>
        /// The server's: her core out of her at <paramref name="from"/>, at the point
        /// <paramref name="at"/>. <paramref name="flight"/> is how many ticks its way takes.
        /// </summary>
        public static int Throw(NPC queen, Vector2 from, Vector2 at, out int flight)
        {
            Vector2 toward = at - from;
            float distance = toward.Length();
            Vector2 line = distance > 1f ? toward / distance : new Vector2(queen.direction == 0 ? 1 : queen.direction, 0f);
            int side = line.X >= 0f ? 1 : -1;

            float reach = MathHelper.Clamp(Math.Max(Reach, distance + Beyond), 200f, MostReach);
            float width = reach * Width;

            // Turned off the line by as much as its way out is off its own axis at that distance.
            float off = distance > 1f && distance < reach ? OffAxis(distance, reach, width) : 0f;
            float axis = line.ToRotation() + side * off;

            flight = Flight(reach);

            return Projectile.NewProjectile(queen.GetSource_FromAI(), from, Vector2.Zero, ModContent.ProjectileType<QueenSlimeCore>(), Damage, 0f,
                Main.myPlayer, queen.whoAmI, axis, side * reach);
        }

        /// <summary>Ticks for the whole way at this reach: from 140 (the nearest) to 300 (the furthest).</summary>
        private static int Flight(float reach) => (int)MathHelper.Clamp(MathHelper.Pi * reach / PeakSpeed, 140f, 300f);

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.ModifyHurtInfo += Exactly;
        }

        /// <summary>The hit's damage, whatever came between: the last word on it.</summary>
        private static void Exactly(ref Player.HurtInfo info) => info.Damage = HitsFor;

        /// <summary>The way in its own frame: along its axis, and out to its side.</summary>
        private static Vector2 Local(float u, float reach, float width)
        {
            float s = (float)Math.Sin(MathHelper.Pi * u);
            return new Vector2(reach * s, width * s * u);
        }

        /// <summary>The angle off its axis of the point on its way out this far from the start. Both grow together to the far end, so halving finds it.</summary>
        private static float OffAxis(float distance, float reach, float width)
        {
            float low = 0f;
            float high = 0.5f;

            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;

                if (Local(middle, reach, width).Length() < distance)
                    low = middle;
                else
                    high = middle;
            }

            Vector2 point = Local((low + high) * 0.5f, reach, width);
            return (float)Math.Atan2(point.Y, point.X);
        }

        /// <summary>Where it is at <paramref name="u"/> of its way, in the world.</summary>
        private Vector2 WayAt(float u)
        {
            float reach = Math.Abs(SignedReach);
            int side = SignedReach >= 0f ? 1 : -1;

            Vector2 along = Axis.ToRotationVector2();
            Vector2 out_ = new Vector2(along.Y, -along.X) * side;
            Vector2 local = Local(u, reach, reach * Width);

            return origin + along * local.X + out_ * local.Y;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            // Its start: where it was made, the first tick it is here.
            if (clock == 0 && origin == Vector2.Zero)
            {
                origin = Projectile.Center;

                if (Main.netMode == NetmodeID.Server)
                    Projectile.netUpdate = true;
            }

            NPC queen = Owner >= 0 && Owner < Main.maxNPCs ? Main.npc[Owner] : null;
            bool hers = queen != null && queen.active && queen.type == NPCID.QueenSlimeBoss;

            // Hers to call back: if she is gone, or no longer waiting for it, it is gone too.
            if (Main.netMode != NetmodeID.MultiplayerClient && (!hers || !LegendsQueenSlime.WaitingForCore(Owner)))
            {
                Projectile.Kill();
                return;
            }

            float reach = Math.Abs(SignedReach);
            float u = Math.Min(1f, clock / (float)Flight(Math.Max(1f, reach)));
            Vector2 at = WayAt(u);

            // The last of the way bends to wherever her core is now.
            if (hers && u > 0.85f)
            {
                float t = MathHelper.Clamp((u - 0.85f) / 0.15f, 0f, 1f);
                at = Vector2.Lerp(at, queen.Center + new Vector2(0f, 2f), t * t * (3f - 2f * t));
            }

            Projectile.velocity = at - Projectile.Center;
            Projectile.Center = at;

            // End over end, and never quite steadily.
            Projectile.rotation += 0.42f + 0.18f * (float)Math.Sin(clock * 0.21f);

            if (!Main.dedServ)
            {
                float glow = Glow();

                Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.2f, 0.3f) * glow);

                if (clock % 8 == 0)
                    SoundEngine.PlaySound(SoundID.Item7 with { Pitch = -0.25f, Volume = 0.8f }, Projectile.Center);

                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, Main.rand.NextFloat(1.1f, 1.6f));
                    dust.noGravity = true;
                    dust.velocity = dust.velocity * 0.3f - Projectile.velocity * 0.1f;
                }
            }

            clock++;

            if (u >= 1f)
                Projectile.Kill();
        }

        /// <summary>Its heat, flickering.</summary>
        private float Glow() => 0.85f + 0.15f * (float)Math.Sin(clock * 0.5f);

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
                dust.velocity *= 1.5f;
            }
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(origin);
            writer.Write((short)Math.Min(clock, short.MaxValue));
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            origin = reader.ReadVector2();

            int told = reader.ReadInt16();

            if (Math.Abs(told - clock) > 4)
                clock = told;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D core = TextureAssets.Projectile[Type].Value;
            Vector2 centre = core.Size() * 0.5f;
            float glow = Glow();

            // Tumbling as well as turning: narrower and wider as it goes over.
            Vector2 scale = new Vector2(0.55f + 0.45f * Math.Abs((float)Math.Cos(clock * 0.37f)), 1f) * Projectile.scale;

            // Where it has been, fading: a red tint over what is behind, and a little red light.
            for (int i = TrailLength - 1; i >= 1; i--)
            {
                Vector2 was = Projectile.oldPos[i];

                if (was == Vector2.Zero)
                    continue;

                float fade = (1f - i / (float)TrailLength) * glow;
                Vector2 at = was + Projectile.Size * 0.5f - Main.screenPosition;
                Vector2 size = scale * (1f - i * 0.03f);

                Main.EntitySpriteDraw(core, at, null, QueenSlimeArt.Red * (0.35f * fade), Projectile.oldRot[i], centre, size, SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(core, at, null, QueenSlimeArt.Additive(QueenSlimeArt.Red, 0.25f * fade), Projectile.oldRot[i], centre, size, SpriteEffects.None, 0f);
            }

            Vector2 here = Projectile.Center - Main.screenPosition;

            // The core, alight, in its red (QueenSlimeArt.DrawRed: its glow first, then its heat on it).
            if (QueenSlimeArt.MorphHalo != null)
            {
                Texture2D halo = QueenSlimeArt.MorphHalo.Value;
                Rectangle frame = QueenSlimeArt.MorphHaloFrame(QueenSlimeArt.MorphFrames - 1);
                Vector2 middle = frame.Size() * 0.5f;

                Main.spriteBatch.Draw(halo, here, frame, QueenSlimeArt.Red * Math.Min(1f, 0.4f * glow), Projectile.rotation, middle, scale * 1.9f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(halo, here, frame, QueenSlimeArt.Red * Math.Min(1f, 0.75f * glow), Projectile.rotation, middle, scale * 1.15f, SpriteEffects.None, 0f);
            }

            Main.EntitySpriteDraw(core, here, null, Color.White, Projectile.rotation, centre, scale, SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(core, here, null, QueenSlimeArt.Additive(QueenSlimeArt.RedHot, 0.55f * glow), Projectile.rotation, centre, scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
