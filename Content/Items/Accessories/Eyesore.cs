using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// Eyesore, from the Eye of Cthulhu in a Legends world, by request ("Summons a looking eye next to you that always
    /// look at the most menacing NPC; looking at it for more than 5 seconds will mark it, First, second and third
    /// attack on that NPC is now 2x, 1,75x, 1.3x. cool down after successful attack 5 seconds; range 45 blocks").
    ///
    /// An eye hangs by the wearer's shoulder (EyesoreEye) and looks at the most menacing thing within 45 tiles of them
    /// (EyesorePlayer.MostMenacing: a boss before anything, then whatever has the most life and hits hardest). Five
    /// seconds on the same one and it is marked: the wearer's next three hits on it do twice, 1.75 times and 1.3 times
    /// their damage. The third spends the mark, and the eye rests five seconds before it will stare again. A mark on
    /// something that dies or gets away is lost, with no rest.
    ///
    /// Mine: what "menacing" is, and that the stare starts over when the eye has to look at something else. The eye
    /// is not shown with the accessory's visibility off; it works all the same. Placeholder pictures
    /// (PlaceholderTextures.md), drawn in code.
    /// </summary>
    public class Eyesore : ModItem
    {
        /// <summary>How far it sees (45 tiles); how long it stares; its rest after a mark is spent.</summary>
        public const float Range = 45f * 16f;
        public const int Stare = 300;
        public const int Rest = 300;

        /// <summary>What the first, second and third hit on a marked one do, times their damage.</summary>
        public static readonly float[] Hits = { 2f, 1.75f, 1.3f };

        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 26;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 3);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            EyesorePlayer eye = player.GetModPlayer<EyesorePlayer>();
            eye.Worn = true;
            eye.Hidden = hideVisual;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = new Color(232, 96, 86),
            });
        }
    }

    /// <summary>Eyesore from the Eye itself, where it drops no bag (Classic), in a Legends world.</summary>
    public class EyesoreDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.EyeofCthulhu;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<Eyesore>()));
            npcLoot.Add(classic);
        }
    }

    /// <summary>Eyesore in the Eye's bag (Expert and Master), in a Legends world.</summary>
    public class EyesoreBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.EyeOfCthulhuBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<Eyesore>()));
        }
    }

    /// <summary>
    /// The wearer's side of Eyesore, on their own machine (where their hits are decided): what the eye looks at, how
    /// long it has, what is marked and how many of its three hits are spent, and the rest after.
    /// </summary>
    public class EyesorePlayer : ModPlayer
    {
        public bool Worn, Hidden;

        /// <summary>The NPC it looks at (-1: nothing), and ticks it has.</summary>
        public int Looking = -1;
        public int Looked;

        /// <summary>The NPC marked (-1: none), and how many hits on it the mark has given.</summary>
        public int Marked = -1;
        public int Spent;

        /// <summary>Ticks of its rest left.</summary>
        public int Resting;

        public override void ResetEffects()
        {
            Worn = false;
            Hidden = false;
        }

        /// <summary>The one an NPC is a part of (a worm's head for its segments), which is what is marked.</summary>
        private static int Whole(NPC npc) => npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? npc.realLife : npc.whoAmI;

        private static bool Seen(NPC npc, Player player) =>
            npc.active && !npc.friendly && !npc.townNPC && npc.lifeMax > 5 && !npc.dontTakeDamage && npc.CanBeChasedBy()
            && Vector2.Distance(npc.Center, player.Center) <= Eyesore.Range;

        /// <summary>How menacing: a boss (or a part of one) over anything; then its life and how hard it hits.</summary>
        private static float Menace(NPC npc)
        {
            NPC whole = Main.npc[Whole(npc)];
            bool boss = whole.boss || NPCID.Sets.ShouldBeCountedAsBoss[whole.type];
            return (boss ? 1e7f : 0f) + whole.lifeMax + whole.damage * 12f;
        }

        /// <summary>The most menacing thing in sight; what it looks at already keeps its place unless something is a fifth more.</summary>
        public int MostMenacing()
        {
            int best = -1;
            float most = 0f;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!Seen(npc, Player))
                    continue;

                float menace = Menace(npc) * (Whole(npc) == Looking ? 1.2f : 1f);

                // Of a thing in parts, the nearest part.
                if (menace > most || menace == most && best >= 0 && Vector2.DistanceSquared(npc.Center, Player.Center) < Vector2.DistanceSquared(Main.npc[best].Center, Player.Center))
                {
                    most = menace;
                    best = npc.whoAmI;
                }
            }

            return best;
        }

        /// <summary>Where the eye should look: a part of the marked one in sight, else the most menacing.</summary>
        public int LookAt { get; private set; } = -1;

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (!Worn || Player.dead)
            {
                Looking = Marked = LookAt = -1;
                Looked = Spent = 0;
                return;
            }

            if (Player.ownedProjectileCounts[ModContent.ProjectileType<EyesoreEye>()] == 0)
                Projectile.NewProjectile(Player.GetSource_Accessory(new Item(ModContent.ItemType<Eyesore>())), Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<EyesoreEye>(), 0, 0f, Player.whoAmI);

            if (Resting > 0)
                Resting--;

            // A mark on something gone or out of sight is lost.
            if (Marked >= 0)
            {
                NPC held = Main.npc[Marked];

                if (!held.active || held.life <= 0 || Vector2.Distance(held.Center, Player.Center) > Eyesore.Range * 1.5f)
                {
                    Marked = -1;
                    Spent = 0;
                }
            }

            int at = MostMenacing();
            LookAt = at;

            if (Marked >= 0)
            {
                // It keeps to what it has marked: the nearest part of it.
                float nearest = float.MaxValue;

                foreach (NPC npc in Main.ActiveNPCs)
                {
                    float d = Vector2.DistanceSquared(npc.Center, Player.Center);

                    if (Whole(npc) == Marked && d < nearest)
                    {
                        nearest = d;
                        LookAt = npc.whoAmI;
                    }
                }

                return;
            }

            int whole = at >= 0 ? Whole(Main.npc[at]) : -1;

            if (Resting > 0 || whole < 0)
            {
                Looking = whole;
                Looked = 0;
                return;
            }

            if (whole != Looking)
            {
                Looking = whole;
                Looked = 0;
                return;
            }

            if (++Looked < Eyesore.Stare)
                return;

            Marked = whole;
            Spent = 0;
            Looked = 0;

            NPC marked = Main.npc[at];
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.6f, Pitch = 0.4f }, marked.Center);
            HitFx.Light(marked.Center, -Vector2.UnitY, new Color(255, 80, 70));
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Worn && Marked >= 0 && Whole(target) == Marked && Spent < Eyesore.Hits.Length)
                modifiers.FinalDamage *= Eyesore.Hits[Spent];
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!Worn || Marked < 0 || Whole(target) != Marked)
                return;

            Vector2 way = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX);
            Color red = new Color(255, 80, 70);

            if (Spent == 0)
                HitFx.Heavy(target.Center, way, red);
            else
                HitFx.Light(target.Center, way, red);

            if (++Spent < Eyesore.Hits.Length)
                return;

            Marked = -1;
            Spent = 0;
            Looking = -1;
            Looked = 0;
            Resting = Eyesore.Rest;
        }
    }

    /// <summary>
    /// Eyesore's eye: by its wearer's shoulder, bobbing, its iris on what it looks at (ai[0]: that NPC and one, 0 for
    /// nothing; ai[1]: how far into its stare, 0 to 1, or 2 and the hits spent when it has marked; both set on the
    /// wearer's machine and told on now and then). As it stares its iris goes from blue to red and a dotted line
    /// grows out to what it stares at; marked, a red eye hangs over that one with a pip for each hit left. Resting,
    /// it is half shut. Hurts nothing.
    /// </summary>
    public class EyesoreEye : ModProjectile
    {
        private const string Art = "PisAndDag/Content/Items/Accessories/";

        public override string Texture => Art + "EyesoreEye";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 2;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            EyesorePlayer eye = player.GetModPlayer<EyesorePlayer>();

            // On the wearer's machine it lives as long as it is worn; elsewhere, as long as it is told about.
            if (Projectile.owner == Main.myPlayer)
            {
                if (!eye.Worn || player.dead || !player.active)
                {
                    Projectile.Kill();
                    return;
                }

                float look = eye.LookAt + 1;
                float state = eye.Marked >= 0 ? 2f + eye.Spent : eye.Resting > 0 ? -1f : eye.Looked / (float)Eyesore.Stare;

                if (look != Projectile.ai[0] || (int)state != (int)Projectile.ai[1] || Main.GameUpdateCount % 30 == 0)
                    Projectile.netUpdate = true;

                Projectile.ai[0] = look;
                Projectile.ai[1] = state;
                Projectile.localAI[0] = eye.Hidden ? 1f : 0f;
            }

            Projectile.timeLeft = 2;

            Vector2 home = player.MountedCenter + new Vector2(-player.direction * 26f, -30f * player.gravDir + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 3f);
            Projectile.Center = Vector2.Distance(Projectile.Center, home) > 400f ? home : Vector2.Lerp(Projectile.Center, home, 0.2f);

            if (Projectile.ai[1] >= 2f)
                Lighting.AddLight(Projectile.Center, 0.35f, 0.08f, 0.06f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.localAI[0] == 1f)
                return false;

            Texture2D ball = TextureAssets.Projectile[Type].Value;
            Texture2D iris = ModContent.Request<Texture2D>(Art + "EyesoreIris").Value;
            Texture2D mark = ModContent.Request<Texture2D>(Art + "EyesoreMark").Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            int looking = (int)Projectile.ai[0] - 1;
            float state = Projectile.ai[1];
            bool marked = state >= 2f, resting = state < 0f;
            float stare = marked ? 1f : resting ? 0f : MathHelper.Clamp(state, 0f, 1f);
            NPC target = looking >= 0 && looking < Main.maxNPCs && Main.npc[looking].active ? Main.npc[looking] : null;

            Vector2 middle = Snap(Projectile.Center);
            Vector2 toward = target != null ? (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) : new Vector2(Main.player[Projectile.owner].direction, 0f);
            Color lit = Color.Lerp(lightColor, Color.White, 0.35f);

            // The line of its stare, growing out to what it stares at.
            if (target != null && !resting && (stare > 0.02f || marked))
            {
                Vector2 from = Projectile.Center + toward * 12f;
                float far = Vector2.Distance(from, target.Center) * (marked ? 1f : stare);
                Color line = new Color(255, 70, 60) * (marked ? 0.5f : 0.15f + 0.4f * stare);
                float run = Main.GlobalTimeWrappedHourly * 30f % 10f;

                for (float d = run; d < far; d += 10f)
                {
                    Vector2 at = Snap(from + toward * d) - Main.screenPosition;
                    Main.EntitySpriteDraw(pixel, at, new Rectangle(0, 0, 2, 2), line, 0f, Vector2.One, 1f, SpriteEffects.None, 0);
                }
            }

            // The ball (shut halfway while it rests), and its iris toward what it looks at.
            Rectangle frame = ball.Frame(1, 2, 0, resting ? 1 : 0);
            Main.EntitySpriteDraw(ball, middle - Main.screenPosition, frame, lit, 0f, frame.Size() * 0.5f, 1f, SpriteEffects.None, 0);

            if (!resting)
            {
                Rectangle irisFrame = iris.Frame(1, 2, 0, stare > 0.5f ? 1 : 0);
                Vector2 off = new Vector2((float)Math.Round(toward.X * 2f) * 2f, (float)Math.Round(toward.Y * 2f) * 2f);
                Main.EntitySpriteDraw(iris, middle + off - Main.screenPosition, irisFrame, lit, 0f, irisFrame.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            }

            // Marked: the red eye over it, and a pip for each hit the mark has left.
            if (marked && target != null)
            {
                int left = Eyesore.Hits.Length - (int)(state - 2f);
                Vector2 over = Snap(target.Top + new Vector2(0f, -22f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 2f));
                Main.EntitySpriteDraw(mark, over - Main.screenPosition, null, Color.White, 0f, mark.Size() * 0.5f, 1f, SpriteEffects.None, 0);

                for (int k = 0; k < left; k++)
                {
                    Vector2 pip = over + new Vector2((k - (left - 1) * 0.5f) * 6f, 12f) - Main.screenPosition;
                    Main.EntitySpriteDraw(pixel, pip, new Rectangle(0, 0, 4, 4), new Color(255, 90, 76), 0f, new Vector2(2f), 1f, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        /// <summary>A world place on the world's two-pixel grid.</summary>
        private static Vector2 Snap(Vector2 at) => new Vector2((float)Math.Floor(at.X / 2f) * 2f, (float)Math.Floor(at.Y / 2f) * 2f);
    }
}
