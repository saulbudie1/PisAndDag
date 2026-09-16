using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Items.Instrument
{
    public class Trumpet : ModItem
    {
        public const int Bands = 12;
        public const int Octaves = 2;

        /// <summary>Longest a press sounds, in ticks, however many bands it crosses.</summary>
        private const int MaxHold = 15 * 60;

        /// <summary>Ticks to fade a note out on release, and to fade the old note under a new one.</summary>
        private const int ReleaseTicks = 7;
        private const int HandoverTicks = 4;

        private const float Loudness = 1f;

        /// <summary>With Alt held: the same note, played soft. Eased into over a few ticks so it does not step.</summary>
        private const float Soft = 0.45f;
        private const float DynamicEase = 0.25f;

        /// <summary>Current playing level, 0..1 of Loudness, following the Alt key.</summary>
        private static float dynamic = 1f;

        /// <summary>Music notes drift off the bell this often while a note sounds.</summary>
        private const int NoteEvery = 9;

        /// <summary>How far the bell lifts while playing, radians. Positive is up for a player facing right.</summary>
        private const float BellLift = 0.28f;

        /// <summary>The world's music stays down this long after the last note, then comes back on its own.</summary>
        public const int MusicRestTicks = 10 * 60;

        /// <summary>The music is pushed down to this, not to nothing: about level with the horn.</summary>
        public const float MusicFloor = 0.4f;

        /// <summary>Tick of the last note sounding; the music waits on it.</summary>
        internal static uint LastNoteTick;

        /// <summary>File names, chromatic from A. Sharps are "s"; the upper octave adds "_up".</summary>
        public static readonly string[] Names =
            { "A", "As", "B", "C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs" };

        public static readonly string[] Labels =
            { "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" };

        private static SoundStyle[] notes;

        // ---- the press that is sounding, one at a time, local player only ----

        internal static Trumpet Playing;

        /// <summary>Band under the cursor right now, 0 at the bottom, or -1 when no trumpet is held. For the ruler.</summary>
        internal static int Hovered = -1;

        /// <summary>Band and octave sounding, or -1.</summary>
        internal static int Band = -1;
        internal static int Octave;

        private static SlotId slot = SlotId.Invalid;
        private static bool rightButton;
        private static int held;
        private static int releasing;

        /// <summary>The note being faded under a handover, and how far along that fade is.</summary>
        private static SlotId fading = SlotId.Invalid;
        private static int fadeTicks;

        // ---- expression: what a player does without thinking about it --------------------
        //
        // All of it comes from timing and the mouse, nothing is a key. Pitch is ridden
        // on the sounding note (ActiveSound.Pitch, -1..1 is an octave down..up) and
        // the level on its Volume, on top of the Alt soft pedal.

        /// <summary>A fresh attack after this much silence scoops up into the note; a quicker re-strike is tongued clean.</summary>
        private const int FreshAfter = 10;

        /// <summary>The scoop: starts this many semitones under and settles over ScoopTicks.</summary>
        private const float ScoopDepth = 0.7f;
        private const int ScoopTicks = 5;

        /// <summary>A fresh note starts here and swells to full over SwellTicks. A tap never gets there.</summary>
        private const float SwellFrom = 0.82f;
        private const int SwellTicks = 90;

        /// <summary>Vibrato: begins after VibratoAfter ticks of holding, grows in over VibratoGrow, at VibratoHz.</summary>
        private const int VibratoAfter = 30;
        private const int VibratoGrow = 40;
        private const float VibratoHz = 5.3f;
        private const float VibratoSemitones = 0.22f;
        private const float VibratoLevel = 0.06f;

        /// <summary>Letting go drops the pitch a touch as the sound dies; flicking the mouse down as you let go makes it a fall.</summary>
        private const float ReleaseDrop = 0.35f;
        private const float FallDrop = 2.5f;
        private const int FallTicks = 12;
        private const int FallFlick = 14;

        private static uint lastEnd;
        private static bool fresh;
        private static float vibratoPhase;
        private static float fall;
        private static int releaseTicks = ReleaseTicks;
        private static readonly int[] mouseY = new int[4];

        /// <summary>An accent hits this much harder for its first AccentTicks.</summary>
        private const float AccentGain = 1.12f;
        private const int AccentTicks = 6;

        /// <summary>The engine refuses a volume past 1; everything that sets one goes through here.</summary>
        private static void SetVolume(ActiveSound sound, float volume)
            => sound.Volume = MathHelper.Clamp(volume, 0f, 1f);

        /// <summary>Pitch, in semitones, to ride on the note this tick.</summary>
        private static float Expression(out float gain)
        {
            float semis = 0f;
            gain = 1f;

            // A score's marks, when one is playing: its dynamics, accents, straight notes.
            bool auto = TrumpetAutoplay.Active;
            bool accent = auto && TrumpetAutoplay.Accent;
            bool straight = auto && TrumpetAutoplay.Straight;

            if (auto)
                gain *= TrumpetAutoplay.Level;

            if (accent && TrumpetAutoplay.NoteAge < AccentTicks)
                gain *= AccentGain;

            if (fresh && !accent)
            {
                if (held < ScoopTicks)
                    semis -= ScoopDepth * (1f - held / (float)ScoopTicks);

                gain *= MathHelper.Lerp(SwellFrom, 1f, MathHelper.Clamp(held / (float)SwellTicks, 0f, 1f));
            }

            if (held > VibratoAfter && !straight)
            {
                float depth = MathHelper.Clamp((held - VibratoAfter) / (float)VibratoGrow, 0f, 1f);
                vibratoPhase += MathHelper.TwoPi * VibratoHz / 60f;
                float wave = (float)Math.Sin(vibratoPhase);
                semis += VibratoSemitones * depth * wave;
                gain *= 1f + VibratoLevel * depth * wave;
            }

            if (releasing > 0)
                semis -= fall * (releasing / (float)releaseTicks);

            return semis;
        }

        /// <summary>Pixels the mouse moved down over the last few ticks; negative is up.</summary>
        private static int Flick()
        {
            for (int i = mouseY.Length - 1; i > 0; i--)
                mouseY[i] = mouseY[i - 1];

            mouseY[0] = PlayerInput.MouseY;
            return mouseY[0] - mouseY[^1];
        }

        public override void SetStaticDefaults()
        {
            notes = new SoundStyle[Bands * Octaves];

            for (int o = 0; o < Octaves; o++)
            {
                for (int i = 0; i < Bands; i++)
                {
                    string file = "PisAndDag/Content/Sounds/Trumpet/Trumpet_" + Names[i] + (o == 1 ? "_up" : "");

                    notes[o * Bands + i] = new SoundStyle(file)
                    {
                        // 15.5 s of real note; MaxHold ends it before the file does.
                        IsLooped = false,
                        Volume = Loudness,
                        MaxInstances = 2,
                        SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
                    };
                }
            }
        }

        public override void Unload()
        {
            notes = null;
            Playing = null;
            slot = SlotId.Invalid;
            fading = SlotId.Invalid;
            Band = -1;
            Hovered = -1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            // Shoot, not HoldUp: HoldUp lifts the item over the head, which is where a
            // torch goes and not where a trumpet does. Shoot puts it in the hand, out
            // in front, turned by itemRotation, which Continue sets to lift the bell.
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.holdStyle = ItemHoldStyleID.HoldFront;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.noMelee = true;
            Item.autoReuse = false;
            Item.UseSound = null;
            // Drawn by TrumpetLayer, pinned by the valves, not by vanilla's corner anchor.
            Item.noUseGraphic = true;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ItemRarityID.Orange;
        }

        /// <summary>The trumpet is not used; it is held and played. See HoldItem.</summary>
        public override bool CanUseItem(Player player) => false;

        // ---- where the horn sits ----------------------------------------------------------
        //
        // The horn is drawn by TrumpetLayer, not by vanilla (noUseGraphic), so that it
        // can be pinned by the point that matters: the valve block, which is where the
        // hand goes. Two points on the sprite, in sprite pixels from its top-left
        // (x toward the bell, y down); change them if the sprite changes.

        /// <summary>The valve block: the middle button, where the hand sits.</summary>
        internal static readonly Vector2 Valves = new Vector2(29f, 12f);

        /// <summary>The mouthpiece: the near edge, half way up the lead pipe.</summary>
        internal static readonly Vector2 Mouthpiece = new Vector2(0f, 8.5f);

        /// <summary>Where the mouthpiece sits while playing: at the chin, from the player's centre, before facing.</summary>
        private static readonly Vector2 Chin = new Vector2(5f, -7f);

        /// <summary>At rest the horn hangs from the hand, bell to the ground. Radians past straight forward.</summary>
        private const float RestHang = 1.15f;

        /// <summary>The held-out arm at rest: straight forward, dropped this much toward the ground.</summary>
        private const float RestArmDroop = 0.35f;

        /// <summary>A sprite offset turned into a world offset for this facing and horn rotation.</summary>
        private static Vector2 Along(Player player, Vector2 spriteOffset)
            => new Vector2(spriteOffset.X * player.direction, spriteOffset.Y).RotatedBy(player.itemRotation);

        /// <summary>The shoulder the front arm swings from, as GetFrontHandPosition reckons it.</summary>
        private static Vector2 Shoulder(Player player)
            => player.MountedCenter + new Vector2(-4f * player.direction, -2f);

        /// <summary>Composite arm rotation that points the arm from the shoulder at a point.</summary>
        private static float ArmToward(Player player, Vector2 point)
        {
            Vector2 v = point - Shoulder(player);
            return (float)Math.Atan2(v.Y, v.X) - MathHelper.PiOver2;
        }

        /// <summary>
        /// The pose, shared by the update (arm, itemRotation) and the draw layer (where
        /// the valves go). Playing: mouthpiece at the chin, bell lifted, arm reaching
        /// for the valves. The arm reaches ten pixels and the valves are twenty-nine
        /// from the mouthpiece, so it points at them rather than landing on them;
        /// bring the valves nearer the mouthpiece on the sprite and the hand closes the
        /// gap by itself. Rest: arm held out, hand on the valves, horn hanging from it.
        /// </summary>
        internal static Vector2 Pose(Player player, bool playing)
        {
            if (playing)
            {
                player.itemRotation = -BellLift * player.direction;
                Vector2 chin = player.Center + new Vector2(Chin.X * player.direction, Chin.Y);
                Vector2 valves = chin + Along(player, Valves - Mouthpiece);
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmToward(player, valves));
                player.itemLocation = valves;
                return valves;
            }

            // Composite rotation is measured from straight down: forward is a quarter turn.
            float arm = (-MathHelper.PiOver2 + RestArmDroop) * player.direction;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, arm);
            player.itemRotation = RestHang * player.direction;
            Vector2 hand = player.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, arm);
            player.itemLocation = hand;
            return hand;
        }

        /// <summary>Called every frame a note sounds, after vanilla has placed the item.</summary>
        public override void UseStyle(Player player, Rectangle heldItemFrame) => Pose(player, true);

        /// <summary>Called every frame the item is held and not in use, because holdStyle is set.</summary>
        public override void HoldStyle(Player player, Rectangle heldItemFrame) => Pose(player, false);

        /// <summary>
        /// Which band the cursor is in: 0 at the bottom of the screen, Bands-1 at the
        /// top. Raw mouse against the real screen, because by this point in the update
        /// Main.mouseX/Y have had the world zoom applied about the centre.
        /// </summary>
        private static int BandUnderCursor()
        {
            // The score says where the cursor is going; the input reads a frame behind it.
            if (TrumpetAutoplay.Active && TrumpetAutoplay.Band >= 0)
                return TrumpetAutoplay.Band;

            int h = Math.Max(PlayerInput.RealScreenHeight, 1);
            int fromTop = Math.Clamp(PlayerInput.MouseY * Bands / h, 0, Bands - 1);

            return Bands - 1 - fromTop;
        }

        public override void HoldItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            TrumpetAutoplay.Tick(player);
            Hovered = BandUnderCursor();

            bool auto = TrumpetAutoplay.Active && TrumpetAutoplay.Pressed;
            bool left = player.controlUseItem || (auto && TrumpetAutoplay.Octave == 0);
            bool right = player.controlUseTile || (auto && TrumpetAutoplay.Octave == 1);

            if (Band < 0)
            {
                // A press over the interface is for the interface. The score is not a press over anything.
                if ((left || right) && (auto || (!player.mouseInterface && !Main.playerInventory)))
                    Start(player, right && !(auto && TrumpetAutoplay.Octave == 0));

                return;
            }

            if (Playing != this)
                return;

            Continue(player, rightButton ? right : left);
        }

        private void Start(Player player, bool right)
        {
            rightButton = right;
            Octave = right ? 1 : 0;
            held = 0;
            releasing = 0;
            Playing = this;
            dynamic = WantsSoft() ? Soft : 1f;

            // A breath before it, or straight off the last note?
            fresh = Main.GameUpdateCount - lastEnd >= (uint)FreshAfter;
            vibratoPhase = 0f;
            fall = 0f;
            releaseTicks = ReleaseTicks;

            Sound(player, BandUnderCursor());
        }

        private static bool WantsSoft() =>
            Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)
            || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt);

        /// <summary>Starts the note for this band, fading whatever was sounding under it.</summary>
        private static void Sound(Player player, int band)
        {
            // Whatever is already fading gets cut; only one handover at a time.
            if (fading.IsValid && SoundEngine.TryGetActiveSound(fading, out ActiveSound old))
                old.Stop();

            fading = slot;
            fadeTicks = 0;

            Band = band;
            slot = SoundEngine.PlaySound(notes[Octave * Bands + band], player.Center);

            if (SoundEngine.TryGetActiveSound(slot, out ActiveSound sound))
            {
                SetVolume(sound, Loudness * dynamic * (fresh && held == 0 ? SwellFrom : 1f));

                // A fresh note starts under the pitch and scoops up; Continue rides it from here.
                if (fresh && held == 0)
                    sound.Pitch = -ScoopDepth / 12f;
            }
        }

        /// <summary>One tick of a press: follow the cursor, ride the fades, keep the pose, and end it.</summary>
        private void Continue(Player player, bool stillHeld)
        {
            // The note under a handover fades out on its own clock.
            if (fading.IsValid)
            {
                if (SoundEngine.TryGetActiveSound(fading, out ActiveSound old) && old.IsPlaying)
                {
                    fadeTicks++;
                    SetVolume(old, Loudness * dynamic * Math.Max(0f, 1f - fadeTicks / (float)HandoverTicks));

                    if (fadeTicks >= HandoverTicks)
                    {
                        old.Stop();
                        fading = SlotId.Invalid;
                    }
                }
                else
                {
                    fading = SlotId.Invalid;
                }
            }

            if (!SoundEngine.TryGetActiveSound(slot, out ActiveSound sound) || !sound.IsPlaying)
            {
                Silence();
                return;
            }

            held++;
            sound.Position = player.Center;
            int flick = Flick();

            // Alt is the soft pedal, and it can be pressed or let go mid-note.
            dynamic = MathHelper.Lerp(dynamic, WantsSoft() ? Soft : 1f, DynamicEase);

            if (releasing == 0 && (!stillHeld || held >= MaxHold))
            {
                releasing = 1;

                // A downward flick as the note ends is a fall, or the score asked for one; otherwise it just settles.
                bool falling = flick >= FallFlick || (TrumpetAutoplay.Active && TrumpetAutoplay.Fall);
                fall = falling ? FallDrop : ReleaseDrop;
                releaseTicks = falling ? FallTicks : ReleaseTicks;
            }

            float semis = Expression(out float gain);
            sound.Pitch = MathHelper.Clamp(semis / 12f, -1f, 1f);
            SetVolume(sound, Loudness * dynamic * gain);

            if (releasing > 0)
            {
                SetVolume(sound, Loudness * dynamic * gain * (1f - releasing / (float)releaseTicks));
                releasing++;

                if (releasing > releaseTicks)
                {
                    sound.Stop();
                    Silence();
                }

                return;
            }

            // Dragged into another band: hand over.
            int under = BandUnderCursor();

            if (under != Band)
                Sound(player, under);

            // The pose, kept alive by hand: vanilla counts these down each tick and
            // drops the arm when they reach zero. Facing the cursor, bell lifted.
            player.itemAnimation = 2;
            player.itemTime = 2;
            player.direction = Main.MouseWorld.X >= player.Center.X ? 1 : -1;
            player.itemRotation = -BellLift * player.direction;
            LastNoteTick = Main.GameUpdateCount;

            if (held % NoteEvery == 1)
                Sing(player);
        }

        /// <summary>A music note drifting off the bell, the way vanilla's instruments do it.</summary>
        private static void Sing(Player player)
        {
            int type = Main.rand.Next(3) switch
            {
                0 => ProjectileID.QuarterNote,
                1 => ProjectileID.EighthNote,
                _ => ProjectileID.TiedEighthNote,
            };

            Vector2 mouth = player.Center + new Vector2(player.direction * 18f, -6f);
            Vector2 drift = new Vector2(player.direction * Main.rand.NextFloat(1.5f, 3f), Main.rand.NextFloat(-2f, -0.5f));

            int i = Projectile.NewProjectile(player.GetSource_ItemUse(player.HeldItem), mouth, drift,
                type, 0, 0f, player.whoAmI);

            if (Main.projectile.IndexInRange(i))
            {
                // Decoration. The harp's notes hurt; these must not.
                Main.projectile[i].friendly = false;
                Main.projectile[i].hostile = false;
            }
        }

        internal static void Silence()
        {
            if (slot.IsValid && SoundEngine.TryGetActiveSound(slot, out ActiveSound sound))
                sound.Stop();

            if (fading.IsValid && SoundEngine.TryGetActiveSound(fading, out ActiveSound old))
                old.Stop();

            if (Band >= 0)
                lastEnd = Main.GameUpdateCount;

            slot = SlotId.Invalid;
            fading = SlotId.Invalid;
            Band = -1;
            releasing = 0;
            held = 0;
            Playing = null;
        }
    }

    /// <summary>
    /// If a note is sounding and the trumpet is no longer what the local player holds
    /// (swapped, dropped, died, left the world), stop it. HoldItem cannot do this for
    /// itself, because it stops being called at exactly that moment. Also clears the
    /// ruler's hover once the trumpet is put away, and hangs the ruler on the interface.
    /// </summary>
    public class TrumpetSystem : ModSystem
    {
        public override void PostUpdatePlayers()
        {
            if (Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            bool holding = player != null && !player.dead && player.HeldItem?.ModItem is Trumpet;

            if (!holding)
            {
                Trumpet.Hovered = -1;
                TrumpetAutoplay.Stop();
            }

            if (Trumpet.Playing != null && (!holding || player.HeldItem.ModItem != Trumpet.Playing))
                Trumpet.Silence();
        }

        /// <summary>
        /// The world's music makes way, down to MusicFloor: still there, about level
        /// with the horn. While a note sounds, and for MusicRestTicks after the last one,
        /// the current track is pushed down each tick; vanilla raises it 0.005 a tick
        /// on its own, so once this stops pushing it comes back over a couple of
        /// seconds without anything more being done.
        /// </summary>
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Trumpet.LastNoteTick == 0)
                return;

            if (Main.GameUpdateCount - Trumpet.LastNoteTick > (uint)Trumpet.MusicRestTicks)
                return;

            int track = Main.curMusic;

            if (track < 0 || track >= Main.musicFade.Length)
                return;

            Main.musicFade[track] = Math.Max(Trumpet.MusicFloor, Main.musicFade[track] - MusicDuckRate);
        }

        private const float MusicDuckRate = 0.06f;

        public override void OnWorldUnload() => Trumpet.Silence();

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Trumpet Ruler",
                () =>
                {
                    TrumpetRuler.Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }

    /// <summary>
    /// The bands, drawn down the right edge while the trumpet is in hand: a tick and a
    /// letter for each, the one under the cursor lit, the sounding one lit harder, and
    /// a word for which octave the press is in. UI space, so the band edges here are
    /// the same fractions of the screen the item reads off the raw mouse.
    /// </summary>
    internal static class TrumpetRuler
    {
        private const int Right = 14;
        private const int TickWidth = 10;
        private const float Scale = 0.8f;

        private static readonly Color Dim = new Color(196, 180, 152) * 0.45f;
        private static readonly Color Lit = new Color(233, 227, 212);
        private static readonly Color Sounding = new Color(240, 178, 150);
        private static readonly Color Line = new Color(18, 14, 20) * 0.5f;

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.hideUI || Trumpet.Hovered < 0)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            int h = Main.screenHeight;
            float bandH = h / (float)Trumpet.Bands;
            int x = Main.screenWidth - Right;

            for (int b = 0; b <= Trumpet.Bands; b++)
            {
                int y = (int)(h - b * bandH);
                spriteBatch.Draw(pixel, new Rectangle(x - TickWidth, y, TickWidth, 1), Line);
            }

            for (int b = 0; b < Trumpet.Bands; b++)
            {
                bool sounding = b == Trumpet.Band;
                bool hovered = b == Trumpet.Hovered;
                Color colour = sounding ? Sounding : hovered ? Lit : Dim;

                string label = Trumpet.Labels[b];
                Vector2 size = font.MeasureString(label) * Scale;
                float cy = h - (b + 0.5f) * bandH;

                if (sounding || hovered)
                    spriteBatch.Draw(pixel, new Rectangle(x - TickWidth - 4, (int)(cy - bandH * 0.5f), TickWidth + 4 + Right, (int)bandH), colour * 0.12f);

                Utils.DrawBorderString(spriteBatch, label, new Vector2(x - TickWidth - 6 - size.X, cy - size.Y * 0.5f), colour, Scale);
            }

            if (Trumpet.Band >= 0)
            {
                string oct = Trumpet.Octave == 1 ? "high" : "low";
                Vector2 size = font.MeasureString(oct) * Scale;
                Utils.DrawBorderString(spriteBatch, oct, new Vector2(x - size.X, 8), Sounding, Scale);
            }
        }
    }

    /// <summary>
    /// Draws the held trumpet in place of vanilla's held-item draw, with the sprite
    /// pinned by its valve block (Trumpet.Valves) at the point Trumpet.Pose chose,
    /// turned by itemRotation, so the hand is on the buttons whatever the angle.
    /// Sits where vanilla's held item would, under the front arm.
    /// </summary>
    public class TrumpetLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            Item held = drawInfo.heldItem;

            return drawInfo.shadow == 0f
                && !player.dead && !player.frozen && !player.JustDroppedAnItem
                && held?.ModItem is Trumpet
                && player.CanVisuallyHoldItem(held);
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            int type = drawInfo.heldItem.type;

            Main.instance.LoadItem(type);
            Texture2D tex = TextureAssets.Item[type].Value;

            // The same pose the update set, recomputed here so the draw never lags a frame.
            Vector2 at = Trumpet.Pose(player, player.itemAnimation > 0);

            SpriteEffects fx = SpriteEffects.None;
            Vector2 origin = Trumpet.Valves;

            if (player.direction == -1)
            {
                fx |= SpriteEffects.FlipHorizontally;
                origin.X = tex.Width - origin.X;
            }

            if (player.gravDir == -1f)
            {
                fx |= SpriteEffects.FlipVertically;
                origin.Y = tex.Height - origin.Y;
            }

            Color light = Lighting.GetColor((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
            Vector2 pos = (at - Main.screenPosition).Floor();

            drawInfo.DrawDataCache.Add(new DrawData(tex, pos, null, drawInfo.heldItem.GetAlpha(light),
                player.itemRotation, origin, player.GetAdjustedItemScale(drawInfo.heldItem), fx));
        }
    }
}
