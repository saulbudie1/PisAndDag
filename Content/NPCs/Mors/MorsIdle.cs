using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using PisAndDag.Content.Structures;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mors
{
    /// <summary>
    /// Mors, standing. Not a fight — a figure in the world that notices you.
    ///
    /// Where and when he stands is MorsPresence's business; what he says is
    /// MorsTalk's. This is only the body.
    ///
    /// Drawn from three layers on one canvas rather than one sprite, so the parts can
    /// move on their own: the body is still, the cloak hangs over it and stirs
    /// (MorsCloak.fx), and the head sits on top and turns to follow the nearest
    /// player. Layer order is body, cloak, head — the cloth is an overcoat, worn
    /// over the armour, and the head goes above everything.
    ///
    /// The head has a neck, not a swivel. It eases toward you rather than snapping,
    /// tilts only so far, and when you climb toward a point nearly overhead the tilt
    /// falls away again — he stops craning and looks straight ahead, the way a man
    /// does when the thing he is watching goes past where his neck will take him.
    ///
    /// Only the head turns. The body is set once, when he appears — toward the
    /// graveyard, for now; later toward whatever the player has made of him — and it
    /// stays put. When you cross behind him the head mirrors about the neck, in
    /// place, so he looks over his shoulder rather than turning round.
    ///
    /// All three PNGs are 68×74 and share the canvas: the same top-left lands every
    /// layer in place, and NeckPivot is one point for all of them. The cloak has 8px
    /// of empty margin on each side, which MorsCloak.fx needs in order to slide
    /// rows sideways without them being cut off at the texture edge. If the sprites
    /// are re-exported, keep that canvas.
    /// </summary>
    public class MorsIdle : ModNPC
    {
        public override string Texture => "PisAndDag/Content/NPCs/Mors/MorsIdle_body";

        // ============================================================
        // THE CANVAS
        // ============================================================

        private const int CanvasWidth = 68;
        private const int CanvasHeight = 74;

        /// <summary>The y edge his soles end on — row 73 is the last with paint.</summary>
        private const int FootLine = 74;

        /// <summary>Where the head turns: the base of the skull, in canvas pixels, art facing left.</summary>
        private static readonly Vector2 NeckPivot = new Vector2(26f, 23f);

        /// <summary>The shoulder line the cloak hangs from, as a fraction of canvas height.</summary>
        private const float CloakAnchorV = 18f / CanvasHeight;

        // ---- the cloak and the weather --------------------------------------------
        //
        // Main.windSpeedCurrent runs to about ±0.35 on an ordinary day and up to ±0.85
        // on a windy one (vanilla calls ≥0.4 windy). Everything scales with the wind,
        // 0 to WindFull, and is capped there: a dead calm still ripples a little, a
        // full gale turns the whole cloth thirty degrees downwind about the shoulders
        // and snaps the hem. Underground there is no wind and he gets the calm.

        /// <summary>Wind at which the cloak is doing everything it can.</summary>
        private const float WindFull = 0.85f;

        /// <summary>The whole cloth's lean downwind in a full gale, about the shoulder line.</summary>
        private const float LeanMax = MathHelper.Pi / 180f * 8f;

        /// <summary>The lean gusts about its mean by this fraction in a gale, so it never sits still.</summary>
        private const float LeanGust = 0.12f;

        /// <summary>Ripple at the hem, calm to gale, texture px.</summary>
        private const float RippleStill = 5f;
        private const float RippleWindy = 10f;

        /// <summary>Sway at the hem in a full gale.</summary>
        private const float SwayMax = 7f;

        /// <summary>Extra lean at the hem, on top of the turn, in a full gale. Small: the margin is 8px.</summary>
        private const float DragMax = 7f;

        /// <summary>Sway rate, radians a second, calm to gale.</summary>
        private const float SwayRateStill = 1.0f;
        private const float SwayRateWindy = 4.2f;

        /// <summary>The ripple runs this much faster in a gale.</summary>
        private const float RippleRateWindy = 4f;

        /// <summary>Hem flutter in a gale, texture px. None in a calm.</summary>
        private const float FlutterMax = 4f;

        /// <summary>The point the cloth turns about: the middle of the shoulder line, on the canvas.</summary>
        private static readonly Vector2 CloakPivot = new Vector2(CanvasWidth * 0.5f, 18f);

        // ============================================================
        // THE NECK
        // ============================================================

        /// <summary>How far up he will look, in radians. About 20 degrees.</summary>
        private const float TiltUpMax = 0.35f;

        /// <summary>How far down. Less — the helm and the hair get in the way.</summary>
        private const float TiltDownMax = 0.18f;

        /// <summary>
        /// Steepness at which he starts to give up craning, and at which he has
        /// given up entirely. Steepness is |dy| / distance: 0 is level with him, 1
        /// is straight overhead. Between these two he lets the tilt fall away.
        /// </summary>
        private const float CraneGiveStart = 0.72f;
        private const float CraneGiveEnd = 0.92f;

        /// <summary>Beyond this he does not bother.</summary>
        private const float Reach = 720f;

        /// <summary>Horizontal dead zone before he turns around, so he does not twitch on the line.</summary>
        private const float TurnDeadZone = 10f;

        /// <summary>Per-tick approach to the wanted tilt. Small: a neck, not a servo.</summary>
        private const float Ease = 0.07f;

        // ============================================================
        // STATE — visual only, never sent
        // ============================================================

        /// <summary>Current tilt, radians. Positive is looking up.</summary>
        private float tilt;

        /// <summary>Which way the head looks: -1 left, the way the art is drawn; +1 mirrored.</summary>
        private int headFacing = -1;

        /// <summary>
        /// Which way the body stands, held in ai[0] so it is the same on every client:
        /// -1 left, +1 right, 0 not yet decided (drawn as left).
        /// </summary>
        private int BodyFacing => NPC.ai[0] > 0f ? 1 : -1;

        private static Asset<Texture2D> cloakTexture;
        private static Asset<Texture2D> headTexture;

        /// <summary>Set by the Mod class if Effects/MorsCloak.xnb loaded. Null means the cloak hangs still.</summary>
        internal static Asset<Effect> CloakEffect;


        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            if (!Main.dedServ)
            {
                cloakTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mors/MorsIdle_cloak");
                headTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Mors/MorsIdle_head");
            }
        }

        public override void Unload()
        {
            cloakTexture = null;
            headTexture = null;
            CloakEffect = null;
        }

        public override void SetDefaults()
        {
            // The name is not on offer — nobody remembers it. The bubble does the
            // pointing instead (Dialogue).
            NPC.ShowNameOnHover = false;

            // Narrower than the paint so he does not snag on steps; the pauldron and the
            // cloak hang outside the box.
            NPC.width = 30;
            NPC.height = 56;

            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.knockBackResist = 0f;

            NPC.friendly = true;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 1000;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.dontCountMe = true;
            NPC.npcSlots = 0f;
            NPC.value = 0f;

            NPC.HitSound = null;
            NPC.DeathSound = null;
        }

        /// <summary>
        /// The body faces the graveyard when he appears, and keeps facing it. Server-side
        /// in multiplayer; ai[0] carries it to the clients. No graveyard means the art's
        /// own way, left.
        /// </summary>
        public override void OnSpawn(IEntitySource source)
        {
            int face = -1;

            if (GraveyardZone.Exists)
            {
                float graveX = (GraveyardZone.Left + GraveyardZone.Right) * 0.5f * 16f;
                face = graveX >= NPC.Center.X ? 1 : -1;
            }

            NPC.ai[0] = face;
            NPC.netUpdate = true;
        }

        public override void AI()
        {
            // He is allowed to despawn like anything else when everyone is far away;
            // MorsPresence puts him back before anyone is near enough to notice.
            NPC.velocity.X = 0f;
            NPC.spriteDirection = BodyFacing;
        }

        public override bool? CanBeHitByItem(Player player, Item item) => false;

        public override bool? CanBeHitByProjectile(Projectile projectile) => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        // ============================================================
        // THE LOOK
        // ============================================================

        /// <summary>
        /// Runs on the drawing client every update, whatever the network is doing, which
        /// is the right place for a thing that is only ever seen.
        /// </summary>
        public override void FindFrame(int frameHeight)
        {
            if (Main.dedServ)
                return;

            Player who = Nearest();

            if (who == null)
            {
                tilt = MathHelper.Lerp(tilt, 0f, Ease);
                return;
            }

            // From the base of his skull, not his feet, or a player standing beside
            // him reads as "below".
            Vector2 neck = NeckWorld();
            Vector2 d = who.Center - neck;

            if (Math.Abs(d.X) > TurnDeadZone)
                headFacing = Math.Sign(d.X);

            float dist = d.Length();

            if (dist < 1f)
            {
                tilt = MathHelper.Lerp(tilt, 0f, Ease);
                return;
            }

            // Angle above level, in the direction he faces. Positive is up.
            float want = (float)Math.Atan2(-d.Y, Math.Abs(d.X));
            want = MathHelper.Clamp(want, -TiltDownMax, TiltUpMax);

            // Nearly overhead: the neck runs out and he lets it go.
            if (d.Y < 0f)
            {
                float steep = -d.Y / dist;
                float give = MathHelper.SmoothStep(0f, 1f, (steep - CraneGiveStart) / (CraneGiveEnd - CraneGiveStart));
                want *= 1f - MathHelper.Clamp(give, 0f, 1f);
            }

            tilt = MathHelper.Lerp(tilt, want, Ease);
        }

        /// <summary>
        /// What the cloth does today, from the wind. The drag is signed: positive wind
        /// blows right, which is +x in the texture when the body faces left and -x when
        /// it is mirrored.
        /// </summary>
        private void Weather(bool bodyFlip, out float ripple, out float rippleRate, out float sway,
            out float swayRate, out float drag, out float flutter, out float lean)
        {
            float wind = NPC.Center.Y > Main.worldSurface * 16f ? 0f : Main.windSpeedCurrent;
            float s = MathHelper.Clamp(Math.Abs(wind) / WindFull, 0f, 1f);
            float downwind = Math.Sign(wind) * (bodyFlip ? -1f : 1f);

            // The gentle things come in early, the violent ones late.
            float soft = MathHelper.SmoothStep(0f, 1f, s);
            float hard = s * s;

            ripple = MathHelper.Lerp(RippleStill, RippleWindy, soft);
            rippleRate = MathHelper.Lerp(1f, RippleRateWindy, hard);
            // The sway wave is the same for every row, so it is worked out here and the
            // shader gets the number: one slow lean with a smaller second beat.
            float rate = MathHelper.Lerp(SwayRateStill, SwayRateWindy, soft);
            float now = Main.GlobalTimeWrappedHourly;
            sway = SwayMax * soft * ((float)Math.Sin(now * rate) * 0.8f + (float)Math.Sin(now * rate * 2.3f + 0.7f) * 0.2f);
            swayRate = rate;
            drag = DragMax * hard * downwind;
            flutter = FlutterMax * hard;

            // The turn about the shoulders, gusting a little so it breathes. Screen
            // clockwise is positive, which is a hem blown to the right.
            float t = Main.GlobalTimeWrappedHourly;
            float gust = 1f + LeanGust * hard * ((float)Math.Sin(t * 2.7f) * 0.7f + (float)Math.Sin(t * 6.1f + 1.0f) * 0.3f);
            lean = LeanMax * hard * gust * Math.Sign(wind);
        }

        /// <summary>The neck's canvas position for the way the body stands.</summary>
        private Vector2 NeckOnCanvas()
        {
            return BodyFacing == 1
                ? new Vector2(CanvasWidth - NeckPivot.X, NeckPivot.Y)
                : NeckPivot;
        }

        /// <summary>The neck in the world.</summary>
        private Vector2 NeckWorld()
        {
            Vector2 topLeft = new Vector2(NPC.Center.X - CanvasWidth * 0.5f, NPC.Bottom.Y - FootLine);
            return topLeft + NeckOnCanvas();
        }

        private Player Nearest()
        {
            Player best = null;
            float bestDist = Reach;

            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead)
                    continue;

                float dist = Vector2.Distance(p.Center, NPC.Center);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }

            return best;
        }

        // ============================================================
        // THE DRAW
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D body = Terraria.GameContent.TextureAssets.Npc[Type].Value;
            Texture2D cloak = cloakTexture.Value;
            Texture2D head = headTexture.Value;

            // Whole pixels, or the layers shimmer against each other as he moves.
            Vector2 topLeft = new Vector2(
                (int)(NPC.Center.X - CanvasWidth * 0.5f - screenPos.X),
                (int)(NPC.Bottom.Y - FootLine - screenPos.Y + NPC.gfxOffY));

            // The body's way is fixed; the head's is its own.
            bool bodyFlip = BodyFacing == 1;
            bool headFlip = headFacing == 1;
            SpriteEffects bodyEffects = bodyFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            SpriteEffects headEffects = headFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 1. Body.
            spriteBatch.Draw(body, topLeft, null, drawColor, 0f, Vector2.Zero, 1f, bodyEffects, 0f);

            // 2. Cloak, over the body, moving if the shader is there.
            if (CloakEffect != null && CloakEffect.IsLoaded)
            {
                Effect effect = CloakEffect.Value;
                Weather(bodyFlip, out float ripple, out float rippleRate, out float sway, out float swayRate,
                    out float drag, out float flutter, out float lean);

                effect.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uTexel"].SetValue(new Vector2(1f / cloak.Width, 1f / cloak.Height));
                effect.Parameters["uAnchorV"].SetValue(CloakAnchorV);
                effect.Parameters["uRipple"].SetValue(ripple);
                effect.Parameters["uRippleRate"].SetValue(rippleRate);
                effect.Parameters["uSway"].SetValue(sway);
                effect.Parameters["uSwayRate"]?.SetValue(swayRate);
                effect.Parameters["uDrag"].SetValue(drag);
                effect.Parameters["uFlutter"]?.SetValue(flutter);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

                // Turned about the shoulders. The origin is in texture space, so a
                // mirrored cloak needs the mirrored pivot to land on the same point.
                Vector2 pivot = bodyFlip ? new Vector2(cloak.Width - CloakPivot.X, CloakPivot.Y) : CloakPivot;
                spriteBatch.Draw(cloak, topLeft + CloakPivot, null, drawColor, lean, pivot, 1f, bodyEffects, 0f);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else
            {
                // No shader: still cloth, but it still leans with the wind.
                Weather(bodyFlip, out _, out _, out _, out _, out _, out _, out float lean);
                Vector2 pivot = bodyFlip ? new Vector2(cloak.Width - CloakPivot.X, CloakPivot.Y) : CloakPivot;
                spriteBatch.Draw(cloak, topLeft + CloakPivot, null, drawColor, lean, pivot, 1f, bodyEffects, 0f);
            }

            // 3. Head, on the neck, turned about it. The neck is wherever the body put
            // it. The origin is in the destination's pixel space, so a mirrored head
            // needs the mirrored pivot to land the same texture pixel on that point —
            // which is also what mirrors it in place, about the neck, rather than
            // across the canvas. A clockwise turn lifts the face when it points left,
            // so the sign flips with it.
            Vector2 neck = topLeft + NeckOnCanvas();
            Vector2 origin = headFlip ? new Vector2(CanvasWidth - NeckPivot.X, NeckPivot.Y) : NeckPivot;
            float rotation = headFlip ? -tilt : tilt;

            spriteBatch.Draw(head, neck, null, drawColor, rotation, origin, 1f, headEffects, 0f);

            return false;
        }
    }
}
