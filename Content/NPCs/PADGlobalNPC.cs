using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Per-NPC mod data, reached with npc.PAD().
    ///
    /// Instanced, so every NPC gets its own copy and a field written here belongs to that
    /// one body. That is the whole reason to have it — PADNPCs above is a singleton and
    /// exists for hooks, and a field on a singleton would silently be shared by every
    /// enemy in the world, which is the kind of bug that only shows up once two of the
    /// same type are on screen.
    ///
    /// Boss bar fields are set from the boss's own SetDefaults, one line each:
    ///
    ///     NPC.PAD().BossName  = "Megashark";
    ///     NPC.PAD().BossTitle = "Built in a shed. Never signed.";
    ///     NPC.PAD().OwnIntro  = true;     // only if the boss animates its own arrival
    ///
    /// No save, no sync: SetDefaults runs wherever the NPC is built, including on a
    /// client that received it over the network, so the fields rebuild themselves.
    /// The globals are instantiated before ModNPC.SetDefaults runs (tML's
    /// GlobalLoaderUtils.SetDefaults), which is what makes writing them from there safe.
    /// </summary>
    public class PADGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// Gives this NPC a boss bar with this name on it. Null or empty means no bar.
        /// Never withheld from the player — see BossPlate.
        /// </summary>
        public string BossName;

        /// <summary>
        /// The line under the name during the arrival only — it rises and flies with the
        /// name and is gone when the name touches the bar. Null means the name goes alone.
        /// Read live, so a boss can change it mid-fight by assigning it again.
        /// </summary>
        public string BossTitle;

        /// <summary>
        /// True when this boss animates its own arrival, so BossIntro's universal one
        /// stays out of the way. Such a boss calls BossIntro.PlayNameOnly itself if it
        /// wants the name and title to land on the bar the same way.
        /// </summary>
        public bool OwnIntro;

        // ---------------------------------------------------------------- CC bypass --
        //
        // See CCBypass. The flag is the state; the rest is the tell.

        /// <summary>
        /// True on any tick this NPC's hit goes through crowd-control immunity. Cleared
        /// before every AI, so the owner sets it each tick it applies and never clears
        /// it. Drives the outline, and on the tick it comes on, the star and the note.
        /// </summary>
        public bool CCBypass;

        /// <summary>
        /// Set once, in SetDefaults, on a part that should not announce itself when its
        /// flag comes on: the fifteen segments of a worm want one star, not fifteen. The
        /// outline is unaffected. The whole calls CCBypass.Warn from the part that should.
        /// </summary>
        public bool CCQuiet;

        private bool wasBypass;

        public override bool PreAI(NPC npc)
        {
            CCBypass = false;
            return true;
        }

        public override void PostAI(NPC npc)
        {
            // The tick it comes on.
            if (CCBypass && !wasBypass && !CCQuiet)
                Combat.CCBypass.Warn(npc);

            wasBypass = CCBypass;
        }

        /// <summary>The outline, under the sprite. Runs before the NPC's own PreDraw.</summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!CCBypass || npc.alpha >= 255)
                return true;

            if (npc.ModNPC is ICCBypassOutline own)
            {
                own.DrawCCBypassOutline(spriteBatch, screenPos);
                return true;
            }

            // Vanilla's own placement for a plain NPC: centred sideways, sat on the
            // bottom of the hitbox plus four, one frame of the sheet, flipped with
            // spriteDirection.
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);

            Vector2 at = new Vector2(
                npc.Center.X - screenPos.X,
                npc.position.Y - screenPos.Y + npc.height + 4f - frame.Height * npc.scale * 0.5f + npc.gfxOffY);

            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Combat.CCBypass.Outline(spriteBatch, texture, frame, at, npc.rotation, origin, npc.scale, flip);
            return true;
        }
    }
}
