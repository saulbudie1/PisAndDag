using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// What the knight lets go of his blades (crescents, the lancet, the votive blades, fire and
    /// light out of the ground): its hit worked out on each machine for that machine's own player,
    /// as his blades are, at the numbers in GildedKnight's tables (the game's own way with a hostile
    /// shot doubles and trebles what it is given; none of that here). Never the game's: damage 0,
    /// so vanilla's hit never fires.
    ///
    /// Each lands on a player once. Whoever spawns one is where the world is decided; its AI runs
    /// on every machine.
    /// </summary>
    public abstract class KnightShot : ModProjectile
    {
        /// <summary>The knight a shot belongs to (its ai[2] is his whoAmI), while his fight is on; null once it is over for him.</summary>
        protected GildedKnight Owner()
        {
            int i = (int)Projectile.ai[2];

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active && npc.ModNPC is GildedKnight knight && !knight.Over ? knight : null;
        }

        /// <summary>The table its damage comes from (GildedKnight's).</summary>
        protected abstract int[] Table { get; }

        /// <summary>The death line's key under Mods.PisAndDag.DeathText.</summary>
        protected virtual string Death => "GildedKnightLight";

        /// <summary>Knockback of its hit.</summary>
        protected virtual float Knock => 5f;

        /// <summary>It can hurt this tick.</summary>
        protected virtual bool Live => true;

        /// <summary>A parry turns it aside (Parry.AddProjectile in its SetStaticDefaults): asked before it lands, from where it comes.</summary>
        protected virtual bool Parried => false;

        /// <summary>Where it comes at them from, for the parry's facing.</summary>
        protected virtual Vector2 ComesFrom => Projectile.Center - Projectile.velocity * 3f;

        /// <summary>Whether it is on this player now (its own shape).</summary>
        protected abstract bool Touches(Player me);

        /// <summary>It has landed on this machine's player.</summary>
        protected bool landed;

        public override string Texture => KnightRig.Folder + "KnightCrescent";

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 120;
            Projectile.aiStyle = -1;
        }

        public override bool CanHitPlayer(Player target) => false;

        public override bool? CanCutTiles() => false;

        /// <summary>Every tick on every machine, after the shot's own AI: its hit on this machine's player.</summary>
        public override void PostAI()
        {
            if (Main.dedServ || landed || !Live)
                return;

            Player me = Main.LocalPlayer;

            // Only on someone in the fight: nothing of his reaches past his walls (a lance of light
            // goes the length of his field and stops at its end).
            if (me == null || !me.active || me.dead || me.immune || !GildedKnight.Fights(me) || !Touches(me))
                return;

            // The parry first, if it can be met by one (and whether or not its wind is over: it comes too fast).
            if (Parried && me.GetModPlayer<Combat.ParryPlayer>().Takes(Projectile, ComesFrom, true))
            {
                landed = true;
                return;
            }

            int direction = me.Center.X > Projectile.Center.X ? 1 : -1;
            int damage = Main.DamageVar(GildedKnight.Damage(Table), -me.luck);

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + Death, me.name));

            if (me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 0f, Knock) > 0)
            {
                landed = true;
                OnLanded(me);
            }
        }

        /// <summary>What the shot does when it has landed on this machine's player (a burst, its end).</summary>
        protected virtual void OnLanded(Player me)
        {
        }

        /// <summary>Whether a line (thick) passes over this player.</summary>
        protected static bool OnLine(Player me, Vector2 from, Vector2 to, float width)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(me.position, me.Size, from, to, width, ref point);
        }

        /// <summary>Whether this player is within a circle.</summary>
        protected static bool InCircle(Player me, Vector2 centre, float radius)
        {
            Vector2 nearest = Vector2.Clamp(centre, me.position, me.position + me.Size);
            return Vector2.DistanceSquared(nearest, centre) <= radius * radius;
        }

        // ---- drawing helpers ----

        protected static Texture2D Tex(string path) => ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad).Value;

        protected static Texture2D Glow => Tex("PisAndDag/Content/Graphics/GlowSoft");
        protected static Texture2D Star => Tex("PisAndDag/Content/Graphics/GlowStar");
        protected static Texture2D Beam => Tex("PisAndDag/Content/Graphics/SoftBeam");
        protected static Texture2D Line => Tex("PisAndDag/Content/Graphics/SoftLine");
        protected static Texture2D Crescent => Tex(KnightRig.Folder + "KnightCrescent");
        protected static Texture2D RingTex => Tex(KnightRig.Folder + "KnightRing");
        protected static Texture2D Pillar => Tex(KnightRig.Folder + "KnightPillar");
        protected static Texture2D Flame => Tex(KnightRig.Folder + "KnightFlame");

        /// <summary>Light added: a colour with its alpha zeroed draws as light over what is there.</summary>
        protected static Color Light(Color c, float strength) => new Color(c.R, c.G, c.B, 0) * strength;

        /// <summary>A soft line of light from one point to another, so thick.</summary>
        protected static void DrawLine(SpriteBatch sb, Texture2D tex, Vector2 from, Vector2 to, float thickness, Color colour)
        {
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 0.5f)
                return;

            sb.Draw(tex, from - Main.screenPosition, null, colour, d.ToRotation(), new Vector2(0f, tex.Height * 0.5f),
                new Vector2(length / tex.Width, thickness / tex.Height), SpriteEffects.None, 0f);
        }

        /// <summary>A glow, so many pixels across, at a point.</summary>
        protected static void DrawGlow(SpriteBatch sb, Vector2 at, float across, Color colour)
        {
            Texture2D tex = Glow;
            sb.Draw(tex, at - Main.screenPosition, null, colour, 0f, tex.Size() * 0.5f, across / tex.Width, SpriteEffects.None, 0f);
        }
    }
}
