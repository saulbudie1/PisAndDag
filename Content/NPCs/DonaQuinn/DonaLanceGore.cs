using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// Dona Quinn's lance, once Triunfo has thrown her: up out of her hand end over end, down,
    /// a bounce and a clatter, and it lies there flat a while before it fades.
    ///
    /// The same picture as the one she holds (Lance_AlsoGore.png, by name). Vanilla's own
    /// gore movement treats every gore as an upright box the size of its picture, which for
    /// something seventy-six pixels long and four wide comes to rest standing on its end; so
    /// this one moves itself (Update returns false): a body at its middle that turns as it
    /// flies, comes off the ground with a third of what it hit it with, skids, and turns the
    /// last little way flat onto the floor.
    ///
    /// Gores are each machine's own; every machine throws its own when she is thrown.
    /// </summary>
    public class DonaLanceGore : ModGore
    {
        public override string Texture => DonaRig.Folder + "Lance_AlsoGore";

        /// <summary>The picture's middle, from its top-left (which is what a gore's position is).</summary>
        private static readonly Vector2 Half = new Vector2(DonaRig.LanceWidth / 2f, DonaRig.LanceLength / 2f);

        private const float Gravity = 0.3f;
        private const float MaxFall = 12f;

        /// <summary>Lying flat, its middle is this far over the floor: the guard holds it up.</summary>
        private const float Lies = 7f;

        /// <summary>A landing faster than this comes off the floor again, with this share.</summary>
        private const float BounceFrom = 2.5f;
        private const float Bounce = 0.33f;

        /// <summary>How long it lies there, and how long it takes to fade at the end of that.</summary>
        private const int Rests = 480;
        private const int Fades = 60;

        /// <summary>While it is in the air its time left is held up here, so the count only starts on the ground.</summary>
        private const int Flying = 100000;

        /// <summary>
        /// Out of her fist as she comes off: from where it was in her hand, turned the way it
        /// was, straight up and a little back the way she came, turning fast, so it comes down
        /// behind her rather than on her.
        /// </summary>
        public static void Throw(NPC npc, DonaRig.Pose seat, int facing)
        {
            if (Main.dedServ)
                return;

            Vector2 middle = seat.FistAt + DonaRig.Turn(new Vector2(0f, DonaRig.LanceLength / 2f - DonaRig.Grip.Y), seat.LanceTurn);
            Vector2 velocity = new Vector2(-facing * Main.rand.NextFloat(0.6f, 1.6f), -Main.rand.NextFloat(8f, 9f));

            int index = Gore.NewGore(npc.GetSource_FromAI(), middle - Half, velocity, ModContent.GoreType<DonaLanceGore>(), 1f);

            if (index < 0 || index >= Main.maxGore)
                return;

            Gore gore = Main.gore[index];
            gore.rotation = seat.LanceTurn;
            gore.timeLeft = Flying;
            gore.alpha = 0;
        }

        public override void OnSpawn(Gore gore, IEntitySource source)
        {
            gore.timeLeft = Flying;
            gore.sticky = false;
            gore.behindTiles = false;
        }

        public override bool Update(Gore gore)
        {
            Vector2 middle = gore.position + Half;

            bool floor = Solid(middle + new Vector2(0f, Lies + Math.Max(0f, gore.velocity.Y)));

            if (floor && gore.velocity.Y >= 0f)
            {
                // Down: onto the floor, up again if it came hard, skidding.
                int top = (int)((middle.Y + Lies + Math.Max(0f, gore.velocity.Y)) / 16f) * 16;
                middle.Y = top - Lies;

                if (gore.velocity.Y > BounceFrom)
                {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.8f, Pitch = -0.2f }, middle);
                    gore.velocity.Y = -gore.velocity.Y * Bounce;
                }
                else
                {
                    gore.velocity.Y = 0f;
                }

                gore.velocity.X *= 0.82f;

                // Flat, whichever way round is nearer.
                float flat = MathHelper.PiOver2 + MathHelper.Pi * (float)Math.Round((gore.rotation - MathHelper.PiOver2) / MathHelper.Pi);
                gore.rotation += (flat - gore.rotation) * 0.25f;
            }
            else
            {
                gore.velocity.Y = Math.Min(gore.velocity.Y + Gravity, MaxFall);

                // End over end, faster the faster it is going sideways.
                gore.rotation += 0.14f * Math.Sign(gore.velocity.X == 0f ? 1f : gore.velocity.X) + gore.velocity.X * 0.05f;
            }

            // Off a wall the other way, with less.
            if (gore.velocity.X != 0f && Solid(middle + new Vector2(Math.Sign(gore.velocity.X) * 6f + gore.velocity.X, 0f)))
                gore.velocity.X *= -0.4f;

            middle += gore.velocity;
            gore.position = middle - Half;

            bool resting = floor && Math.Abs(gore.velocity.X) < 0.15f && gore.velocity.Y == 0f;

            if (resting && gore.timeLeft > Rests)
                gore.timeLeft = Rests;

            if (gore.timeLeft <= Rests)
            {
                gore.timeLeft--;

                if (gore.timeLeft < Fades)
                    gore.alpha = (int)MathHelper.Clamp((Fades - gore.timeLeft) * 255f / Fades, 0f, 255f);

                if (gore.timeLeft <= 0)
                    gore.active = false;
            }

            // Out of the world, or lost in the air for ever: gone.
            if (middle.Y > Main.maxTilesY * 16f || middle.X < 0f || middle.X > Main.maxTilesX * 16f)
                gore.active = false;

            return false;
        }

        /// <summary>Whether the tile at this point is something to land on.</summary>
        private static bool Solid(Vector2 at)
        {
            int x = (int)(at.X / 16f);
            int y = (int)(at.Y / 16f);

            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }
    }
}
