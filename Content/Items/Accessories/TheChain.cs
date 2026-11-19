using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The Chain (by request: "Legends only accessory: The Chain. 9 body segments of shrunk down destroyer with the tail,
    /// stretch and whip the npc max 25 tiles behind the body, dealing 150 damage, each count as a 'Combo Xn' with n being
    /// number of hit at n = 5 it becomes 'Electrifying' and whip (like vanilla whip) at the npc with some electricity effect
    /// dealing 200 damage and stun apply 3 seconds of electrified debuff, idle its just curled up like an S behind the body
    /// like squirrel tail"; asked after: from the Legends Destroyer, every 1.25 s, the game's own Electrified shortened to 2
    /// s, stunning all but bosses for its time and taking 20 life a second (ElectrifiedNPC), the game's own Destroyer
    /// pictures shrunk; and "somewhat lights up the dark, a red glow, or that cyan at combo x4").
    ///
    /// Worn: nine body pieces and the tail of Destroyer-Lite, the Deactivated Probe's pet (TheChainTail), curl behind its
    /// wearer in an S. Every 1.25 seconds it lashes the nearest enemy within 25 tiles, as a whip does, for 150 ("Combo X1"
    /// to "X4"); the fifth is "Electrifying": a slower, wider crack, its lightning down its length, 200 and the Electrified
    /// debuff for 2 seconds. A combo lapses after 4 seconds without a hit. It lights the dark about it, red, and
    /// in its colour (#7fd4d1) once the next strike will be the fifth.
    ///
    /// Only in a Legends world: from the Destroyer itself where it drops no bag, in its bag where it does (both
    /// <see cref="LegendsOnly"/>), every time. A "Legends" line under its tooltip, in its colour.
    /// </summary>
    public class TheChain : ModItem
    {
        public override string LocalizationCategory => "Items.Accessories";

        /// <summary>Drawn by hand from Destroyer-Lite's pictures (PreDrawInInventory, PreDrawInWorld): this is only its size.</summary>
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DestroyerPet;

        public const float Range = 25f * 16f;
        public const int Every = 75;
        public const int Damage = 150;
        public const int WhipDamage = 200;
        public const int ElectrifiedTicks = 120;
        public const int ComboLapse = 240;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.accessory = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 6);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            ChainPlayer chain = player.GetModPlayer<ChainPlayer>();
            chain.Worn = true;
            chain.Hidden = hideVisual;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = DestroyerArt.Teal,
            });
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            TheChainTail.DrawCurl(spriteBatch, position, 30f * scale, Color.White, 1);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            TheChainTail.DrawCurl(spriteBatch, Item.Center - Main.screenPosition, 30f * scale, lightColor, 1);
            return false;
        }
    }

    /// <summary>Its wearer's: worn, hidden, its combo and how long it holds.</summary>
    public class ChainPlayer : ModPlayer
    {
        public bool Worn, Hidden;

        /// <summary>Hits in its combo so far (the fifth strike is the electrifying one), and ticks till it lapses.</summary>
        public int Combo;
        public int ComboLeft;

        public override void ResetEffects()
        {
            Worn = false;
            Hidden = false;
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (!Worn || Player.dead)
            {
                Combo = 0;
                ComboLeft = 0;
                return;
            }

            if (Player.ownedProjectileCounts[ModContent.ProjectileType<TheChainTail>()] == 0)
                Projectile.NewProjectile(Player.GetSource_Accessory(new Item(ModContent.ItemType<TheChain>())), Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<TheChainTail>(), TheChain.Damage, 2f, Player.whoAmI);

            if (ComboLeft > 0 && --ComboLeft == 0)
                Combo = 0;
        }
    }

    /// <summary>
    /// The Chain's tail: nine body pieces and the tail of Destroyer-Lite (the Deactivated Probe's pet: its own pictures,
    /// Projectile 887, and its glow, GlowMask 275, by request: "use deactivated probe's mech worm pet textures instead of
    /// shrunk down destroyer"), behind its wearer.
    ///
    ///   IDLE   curled in an S behind the wearer's lower back, swaying, its pieces following where they should be
    ///   LASH   (hits one to four) a whip's lash (by request: "check vanilla whip why is this thing tapping ... i need it to
    ///          whip"), after the game's own whips (Projectile.FillWhipControlPoints): it starts curled behind, uncurls as
    ///          it swings over the wearer's head, its root leading and its tip trailing, and is straight at the crack (0.62
    ///          of the way), its full 25 tiles toward the enemy every time; then it flicks past, draws in, and curls back
    ///          to its rest. 30 ticks. It hurts along its whole length as it swings through (each enemy once a lash); its
    ///          first hit of a lash counts for its combo.
    ///   CRACK  (the fifth) the same lash, slower and wider, swung from further back, its lightning down its length; 42.
    ///
    /// Each piece is laid along from its root: its root's heading swings from behind to the enemy, and each piece turns
    /// from the one before by the lash's curl, which goes to nothing at the crack.
    ///
    /// The wearer's machine picks the target and the moment and says so (ai); every machine moves and draws it from that.
    /// </summary>
    public class TheChainTail : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DestroyerPet;

        public const int Pieces = 10;

        /// <summary>The pet's own spacing (Main.DrawProj_..., its segments 16 apart).</summary>
        public const float Spacing = 16f;

        private const int Idle = 0, Lash = 1, Crack = 2;

        private const int LashTicks = 30, CrackTicks = 42;

        /// <summary>How far through a lash it cracks (straight, its tip on the enemy).</summary>
        private const float CrackAt = 0.62f;

        /// <summary>Its rest: how each piece lies from the wearer's lower back, the wearer facing left (behind is +x, up is -y).</summary>
        private static readonly Vector2[] Rest = MakeRest();

        private static Vector2[] MakeRest()
        {
            float[] turns = { -30f, -40f, -35f, -25f, -10f, 10f, 30f, 40f, 40f, 35f };
            Vector2[] points = new Vector2[Pieces];
            Vector2 at = Vector2.Zero;
            float heading = MathHelper.ToRadians(20f);

            for (int i = 0; i < Pieces; i++)
            {
                heading += MathHelper.ToRadians(turns[i]);
                at += heading.ToRotationVector2() * Spacing;
                points[i] = at;
            }

            return points;
        }

        private int Mode => (int)Projectile.ai[0];

        private int Target => (int)Projectile.ai[1];

        /// <summary>Its wearer's combo, as its wearer's machine last said (for its glow).</summary>
        private int Combo => (int)Projectile.ai[2];

        /// <summary>Where each piece is (this machine's), and whether they have been put anywhere yet; its lash's shape this tick.</summary>
        private readonly Vector2[] pieces = new Vector2[Pieces];
        private readonly Vector2[] shape = new Vector2[Pieces];
        private bool placed;

        /// <summary>Ticks into its lash (this machine's), the mode it was in, and its wait till the next (its wearer's).</summary>
        private int clock;
        private int lastMode = -1;
        private int wait = 30;

        /// <summary>Which way its lash swings over (toward the enemy's side), set as it begins.</summary>
        private float side = 1f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = CrackTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>Its root: the wearer's lower back.</summary>
        private static Vector2 Root(Player player) => player.MountedCenter + new Vector2(-player.direction * 6f, 6f + player.gfxOffY);

        /// <summary>Where a piece of its rest lies now, with its sway.</summary>
        private static Vector2 RestAt(Player player, int i, float time)
        {
            Vector2 local = Rest[i];
            float sway = (float)Math.Sin(time * 2.1f - i * 0.45f) * (1.5f + i * 0.35f);
            local += new Vector2(sway * 0.4f, sway * 0.6f);
            local = local.RotatedBy(Math.Sin(time * 1.3f) * 0.05f);
            return Root(player) + new Vector2(local.X * player.direction * -1f, local.Y);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private int Length => Mode == Crack ? CrackTicks : LashTicks;

        /// <summary>How far through its lash it is (0 to 1).</summary>
        private float Through => Math.Min(1f, (clock + 1f) / Length);

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            ChainPlayer chain = player.GetModPlayer<ChainPlayer>();

            // On the wearer's machine it lives as long as it is worn; elsewhere, as long as it is told about.
            if (Projectile.owner == Main.myPlayer && (!chain.Worn || player.dead || !player.active))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Vector2 root = Root(player);
            Projectile.Center = root;
            float time = (float)Main.timeForVisualEffects / 60f;

            if (!placed)
            {
                for (int i = 0; i < Pieces; i++)
                    pieces[i] = RestAt(player, i, time);

                placed = true;
            }

            if (Mode != lastMode)
            {
                clock = 0;
                lastMode = Mode;
                counted = false;

                // Which way it swings over: toward the enemy's side (worked out alike on every machine).
                if (Mode != Idle && Target >= 0 && Target < Main.maxNPCs)
                {
                    side = Main.npc[Target].Center.X >= player.Center.X ? 1f : -1f;

                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item152 with { Volume = 0.6f, Pitch = Mode == Crack ? -0.25f : 0.15f }, root);
                }
            }

            if (Projectile.owner == Main.myPlayer)
                Decide(player, chain);

            NPC target = Target >= 0 && Target < Main.maxNPCs && Main.npc[Target].active ? Main.npc[Target] : null;
            bool lashing = Mode != Idle && target != null;

            if (lashing)
                LashShape(root, target.Center, Through, Mode == Crack, shape);

            // Into its lash from its rest over its first ticks, out of it back to its rest after the crack.
            float u = Through;
            float into = !lashing ? 0f : u <= CrackAt ? Math.Min(1f, (clock + 1f) / 4f) : 1f - Smooth((u - CrackAt) / (1f - CrackAt));
            float follow = lashing ? 0.65f : 0.3f;

            for (int i = 0; i < Pieces; i++)
            {
                Vector2 want = RestAt(player, i, time);

                if (lashing)
                    want = Vector2.Lerp(want, shape[i], into);

                pieces[i] = Vector2.Lerp(pieces[i], want, follow);
            }

            // Held together: none further from the one before than it may stretch, so it reads as a chain.
            float most = Mode == Idle ? Spacing * 1.3f : TheChain.Range / Pieces * 1.15f;
            Vector2 before = root;

            for (int i = 0; i < Pieces; i++)
            {
                Vector2 off = pieces[i] - before;

                if (off.Length() > most)
                    pieces[i] = before + off.SafeNormalize(Vector2.UnitY) * most;

                before = pieces[i];
            }

            Effects(player, target);
            clock++;
        }

        // ---- its wearer's machine: when, and what ------------------------------------------------------------------------

        private void Decide(Player player, ChainPlayer chain)
        {
            Projectile.damage = (int)player.GetTotalDamage(DamageClass.Generic).ApplyTo(TheChain.Damage);

            if (Combo != chain.Combo)
            {
                Projectile.ai[2] = chain.Combo;
                Projectile.netUpdate = true;
            }

            if (Mode != Idle)
            {
                bool lost = Target < 0 || Target >= Main.maxNPCs || !Main.npc[Target].active;

                if (clock >= Length || lost)
                {
                    Projectile.ai[0] = Idle;
                    Projectile.ai[1] = -1;
                    Projectile.netUpdate = true;
                }

                return;
            }

            if (wait > 0)
            {
                wait--;
                return;
            }

            int pick = Nearest(player);

            if (pick < 0)
                return;

            wait = TheChain.Every;
            Projectile.ai[0] = chain.Combo >= 4 ? Crack : Lash;
            Projectile.ai[1] = pick;
            Projectile.netUpdate = true;
        }

        /// <summary>The nearest enemy it can reach (within 25 tiles, in sight of its wearer).</summary>
        private static int Nearest(Player player)
        {
            int best = -1;
            float nearest = TheChain.Range;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy())
                    continue;

                float d = Vector2.Distance(npc.Center, player.Center);

                if (d < nearest && Collision.CanHitLine(player.Center, 1, 1, npc.Center, 1, 1))
                {
                    nearest = d;
                    best = npc.whoAmI;
                }
            }

            return best;
        }

        // ---- its lash ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Its lash's shape at <paramref name="u"/> (0 to 1) toward <paramref name="at"/>. Before the crack its root's
        /// heading swings from behind the wearer over their head to the enemy, it lengthens, and each piece turns back
        /// from the one before (the tip trailing), less and less, till at the crack it lies straight to the enemy. After,
        /// its root swings a little past, its tip flicks, and it draws in.
        /// </summary>
        private void LashShape(Vector2 root, Vector2 at, float u, bool electric, Vector2[] into)
        {
            // Its whole length every time, 25 tiles, as a whip has its reach (by request: "its standard whip length 25 tiles every time").
            float far = TheChain.Range;
            float aim = (at - root).ToRotation();
            float swing = MathHelper.ToRadians(electric ? 170f : 150f);
            float past = MathHelper.ToRadians(30f);
            float heading, curl, reach;

            if (u <= CrackAt)
            {
                float s = u / CrackAt;
                heading = aim - side * swing * (1f - Smooth(s));
                curl = -side * 0.55f * (float)Math.Pow(1f - s, 1.2f);
                reach = 0.3f + 0.7f * (float)Math.Pow(s, 0.8f);
            }
            else
            {
                float v = (u - CrackAt) / (1f - CrackAt);
                heading = aim + side * past * Smooth(v);
                curl = side * 0.12f * (float)Math.Sin(MathHelper.Pi * Math.Min(1f, v * 1.6f));
                reach = 1f - 0.35f * Smooth(v);
            }

            float piece = far / Pieces * reach;
            Vector2 p = root;

            for (int i = 0; i < Pieces; i++)
            {
                p += heading.ToRotationVector2() * piece;
                into[i] = p;
                heading += curl;
            }
        }

        /// <summary>It hurts as it swings through: from over the wearer's head, through the crack, to a little after.</summary>
        private bool InHit => Mode != Idle && Through >= CrackAt - 0.25f && Through <= CrackAt + 0.12f;

        /// <summary>Its combo has been counted for this lash (its first hit); its wearer's machine's.</summary>
        private bool counted;

        // ---- hitting ----------------------------------------------------------------------------------------------------------

        public override bool? CanHitNPC(NPC target) => InHit ? null : false;

        /// <summary>Along its whole length, as a whip's is.</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 before = Root(Main.player[Projectile.owner]);
            float along = 0f;

            for (int i = 0; i < Pieces; i++)
            {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), before, pieces[i], 18f, ref along))
                    return true;

                before = pieces[i];
            }

            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Mode == Crack)
                modifiers.SourceDamage *= TheChain.WhipDamage / (float)TheChain.Damage;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[Projectile.owner];
            ChainPlayer chain = player.GetModPlayer<ChainPlayer>();
            string said;
            Color colour;

            if (Mode == Crack)
                target.AddBuff(BuffID.Electrified, TheChain.ElectrifiedTicks);

            // Anything else it catches in the same lash is only hurt (and electrified); the lash's first hit is its combo's.
            if (counted)
            {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);

                if (Mode == Crack)
                    Electrify(target.Center);

                return;
            }

            counted = true;

            if (Mode == Crack)
            {
                chain.Combo = 0;
                chain.ComboLeft = 0;
                said = Mod.GetLocalization("Items.Accessories.TheChain.Electrifying").Value;
                colour = DestroyerArt.Teal;
                Electrify(target.Center);
            }
            else
            {
                chain.Combo = Math.Min(4, chain.Combo + 1);
                chain.ComboLeft = TheChain.ComboLapse;
                said = Mod.GetLocalization("Items.Accessories.TheChain.Combo").Format(chain.Combo);
                colour = chain.Combo >= 4 ? DestroyerArt.Teal : new Color(255, 96, 80);
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.5f, Pitch = 0.2f + 0.05f * chain.Combo }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.3f + 0.1f * chain.Combo }, target.Center);
            }

            CombatText.NewText(target.Hitbox, colour, said, dramatic: Mode == Crack);
            Projectile.ai[2] = chain.Combo;
            Projectile.netUpdate = true;
        }

        /// <summary>Its electrifying crack, on the enemy: a flash, its lightning out of the tip, sparks, the crack and the zap.</summary>
        private static void Electrify(Vector2 at)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, DestroyerArt.Teal * 0.9f, 1.1f, 16) { Pixelate = true });
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White * 0.7f, 0.4f, 9) { Pixelate = true });

            for (int i = 0; i < 4; i++)
                Lightning.Spawn(at, at + Main.rand.NextVector2Circular(90f, 90f), i == 0 ? Color.White * 0.8f : DestroyerArt.Teal, Main.rand.NextFloat(8f, 13f), 16, 1);

            for (int i = 0; i < 8; i++)
                PADParticleHandler.SpawnParticle(new Ember(at, Main.rand.NextVector2Circular(5f, 5f) + new Vector2(0f, -1f), DestroyerArt.TealHot, Main.rand.NextFloat(0.45f, 0.7f), Main.rand.Next(16, 28)) { Pixelate = true });

            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.95f }, at);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.7f, Pitch = 0.1f }, at);
            Main.LocalPlayer.PIS().BumpScreenShake(3f, 10);
        }

        // ---- light, lightning ----------------------------------------------------------------------------------------------

        /// <summary>Whether it glows in its colour: the next strike is the fifth, or it is the fifth.</summary>
        private bool Charged => Combo >= 4 || Mode == Crack;

        private void Effects(Player player, NPC target)
        {
            if (Main.dedServ)
                return;

            // It lights the dark about it a little: red, or its colour when charged.
            Vector3 light = Charged ? new Vector3(0.22f, 0.5f, 0.5f) : new Vector3(0.42f, 0.07f, 0.05f);

            for (int i = 1; i < Pieces; i += 2)
                Lighting.AddLight(pieces[i], light);

            // Its crack, its lightning down its length; charged, now and then a crackle along it.
            if (Mode == Crack && clock % 2 == 0)
            {
                int i = Main.rand.Next(Pieces - 1);
                Lightning.Spawn(pieces[i], pieces[i + 1] + Main.rand.NextVector2Circular(6f, 6f), DestroyerArt.Teal, 5f, 8, 0);
            }
            else if (Charged && Main.rand.NextBool(40))
            {
                int i = Main.rand.Next(Pieces - 1);
                Lightning.Spawn(pieces[i], pieces[i] + Main.rand.NextVector2Circular(16f, 16f), DestroyerArt.Teal, 3f, 8, 0);
            }
        }

        // ---- drawing ------------------------------------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            if (!placed || player.GetModPlayer<ChainPlayer>().Hidden && Mode == Idle)
                return false;

            DrawPieces(Main.spriteBatch, pieces, Root(player), 1f, Charged, light: true);
            return false;
        }

        /// <summary>
        /// Its pieces, tail first: Destroyer-Lite's body (its sheet's second frame) and tail (its last), turned and flipped
        /// as the game draws the pet, each facing the one before it (the first, <paramref name="root"/>); its glow over
        /// them, pulsing as the pet's does, or in its colour when charged. World places (less the screen), lit where they
        /// are, or full bright for an icon.
        /// </summary>
        public static void DrawPieces(SpriteBatch batch, Vector2[] at, Vector2 root, float scale, bool charged, bool light, Vector2? offset = null)
        {
            Main.instance.LoadProjectile(ProjectileID.DestroyerPet);
            Texture2D sheet = TextureAssets.Projectile[ProjectileID.DestroyerPet].Value;
            Texture2D glow = TextureAssets.GlowMask[GlowMaskID.DestroyerPet].Value;
            Texture2D tealGlow = charged ? DestroyerArt.TealOf(glow) : glow;
            int frames = Math.Max(1, Main.projFrames[ProjectileID.DestroyerPet]);
            Vector2 shift = offset ?? -Main.screenPosition;
            Color pulse = charged ? new Color(255, 255, 255, 0) : Color.White * (Main.mouseTextColor / 255f);

            for (int i = at.Length - 1; i >= 0; i--)
            {
                bool last = i == at.Length - 1;
                Rectangle frame = sheet.Frame(1, frames, 0, last ? frames - 1 : Math.Min(1, frames - 1));
                Vector2 ahead = i == 0 ? root : at[i - 1];
                float rotation = (ahead - at[i]).ToRotation() + MathHelper.PiOver2;
                SpriteEffects flip = at[i].X < ahead.X ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Color lit = light ? Lighting.GetColor((int)(at[i].X / 16f), (int)(at[i].Y / 16f)) : Color.White;
                Vector2 place = at[i] + shift;
                Vector2 origin = frame.Size() * 0.5f;

                batch.Draw(sheet, place, frame, lit, rotation, origin, scale, flip, 0f);
                batch.Draw(tealGlow, place, frame, pulse, rotation, origin, scale, flip, 0f);
            }
        }

        /// <summary>Its icon: its rest's S, fitted into <paramref name="size"/> pixels about <paramref name="centre"/> (a screen place).</summary>
        public static void DrawCurl(SpriteBatch batch, Vector2 centre, float size, Color tint, int facing)
        {
            Vector2 min = Vector2.Zero, max = Vector2.Zero;

            foreach (Vector2 p in Rest)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Vector2 span = max - min + new Vector2(16f);
            float fit = size / Math.Max(span.X, span.Y);
            Vector2 middle = (min + max) * 0.5f;
            Vector2[] points = new Vector2[Pieces];

            for (int i = 0; i < Pieces; i++)
                points[i] = centre + (Rest[i] - middle) * fit * new Vector2(facing, 1f);

            Vector2 root = centre + (Vector2.Zero - middle) * fit * new Vector2(facing, 1f);
            DrawPieces(batch, points, root, fit, false, light: false, offset: Vector2.Zero);
        }
    }

    /// <summary>The Chain from the Destroyer itself, where it drops no bag (Classic), in a Legends world.</summary>
    public class TheChainDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.TheDestroyer;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<TheChain>()));
            npcLoot.Add(classic);
        }
    }

    /// <summary>The Chain in the Destroyer's bag (Expert and Master), in a Legends world.</summary>
    public class TheChainBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.DestroyerBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<TheChain>()));
        }
    }
}
