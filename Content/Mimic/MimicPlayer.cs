using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The Everclear Mirror's, on both sides of it.
    ///
    /// On the copy (MimicSystem): its life three times its owner's, worked out last of all (after every accessory, set
    /// and buff), and full the first time it is known; no hurt from a fall; at no life it does not die as a player
    /// dies, it breaks (<see cref="Broken"/>, which MimicSystem answers); the faint trail of itself it leaves when it
    /// moves (the game's own, as some armour leaves); and the jump the game gave it this tick, for its brain.
    ///
    /// On its owner: how long the mirror stays clouded after a copy broke (<see cref="Cloud"/>).
    /// </summary>
    public class MimicPlayer : ModPlayer
    {
        /// <summary>The copy's life, to its owner's (by request: "its HP is Triple player's HP").</summary>
        public const int LifeTimes = 3;

        // By request, for each of the owner's minion slots past the mirror's twelve (MimicSystem.Extra, n): "life = player
        // life * 3 + n*(0.15)" (read as three times, and 0.15 times more for each), "defense = player.def * 1.5 + n * 5",
        // "damage = damage * (1 + n*0.2)".
        public const float LifePerSlot = 0.15f;
        public const float DefenseTimes = 1.5f;
        public const int DefensePerSlot = 5;
        public const float DamagePerSlot = 0.2f;

        /// <summary>The copy, just made: its life filled once it is known.</summary>
        internal bool Fresh;

        /// <summary>The copy's life ran out.</summary>
        internal bool Broken;

        /// <summary>The jump the game gave the copy this tick (Player.jumpSpeed and jumpHeight, the game's, set for each player as it is run).</summary>
        internal float JumpSpeed = 5.01f;

        internal int JumpTicks = 15;

        /// <summary>The owner's: ticks the mirror stays clouded.</summary>
        internal int Cloud;

        /// <summary>The owner's: the last thing they hit, and when (the copy goes for it too).</summary>
        internal int LastHit = -1;
        internal uint LastHitAt;

        /// <summary>
        /// What the copy has learned of each weapon (MimicSystem.Learned: from how far each lands the most, which way of
        /// using it), kept with its owner, so what it learned in one session (against a target dummy, say) it starts the
        /// next with. Each weapon by its mod and name ("Terraria/n" for the game's), so other mods' coming and going does
        /// not muddle them.
        /// </summary>
        /// <summary>What was saved with this player, until a copy of theirs is made (MimicSystem takes it up then).</summary>
        internal System.Collections.Generic.IList<TagCompound> Learned;

        public override void SaveData(TagCompound tag)
        {
            if (Player.whoAmI == MimicSystem.Slot)
                return;

            // What the copy knows now, if it was this player's; else what was loaded with them, kept as it was.
            if (MimicSystem.LearnedFor == Player)
            {
                var list = new System.Collections.Generic.List<TagCompound>();

                foreach (var (id, row) in MimicSystem.Learned.Export())
                {
                    string key = KeyOf(id);

                    if (key != null)
                        list.Add(new TagCompound { ["k"] = key, ["v"] = new System.Collections.Generic.List<float>(row) });
                }

                Learned = list;
            }

            if (Learned != null)
                tag["mirrorLearned"] = Learned;
        }

        public override void LoadData(TagCompound tag)
        {
            Learned = tag.ContainsKey("mirrorLearned") ? tag.GetList<TagCompound>("mirrorLearned") : null;
        }

        /// <summary>Puts what was saved with this player into what the copy knows (MimicSystem, as a copy of theirs is made).</summary>
        internal void Teach(Practice into)
        {
            into.Clear();

            if (Learned == null)
                return;

            foreach (TagCompound e in Learned)
            {
                int id = IdOf(e.GetString("k"));

                if (id > 0)
                    into.Import(id, System.Linq.Enumerable.ToArray(e.GetList<float>("v")));
            }
        }

        private static string KeyOf(int id)
        {
            if (id > 0 && id < Terraria.ID.ItemID.Count)
                return "Terraria/" + id;

            ModItem m = id >= Terraria.ID.ItemID.Count ? ItemLoader.GetItem(id) : null;
            return m == null ? null : m.Mod.Name + "/" + m.Name;
        }

        private static int IdOf(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            if (key.StartsWith("Terraria/"))
                return int.TryParse(key.Substring(9), out int v) ? v : 0;

            return ModContent.TryFind(key, out ModItem m) ? m.Type : 0;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Hit(target, damageDone, -1);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) => Hit(target, damageDone, MimicShots.TagOf(proj));

        /// <summary>
        /// The copy uses every weapon again for as long as it holds the button, as the game does for anyone with "auto swing"
        /// on (Player.CanAutoReuseItem with autoReuseAllWeapons: a weapon with damage, not while it channels one). The game
        /// sets autoReuseAllWeapons from that setting each tick (Player.Update), over what MimicSystem gave the copy, so with
        /// the setting off a sword without auto reuse of its own swung once for every bout of holding (round 81: "it is not
        /// knowing to spam").
        /// </summary>
        public override bool? CanAutoReuseItem(Item item)
        {
            if (!MimicSystem.Is(Player) || item == null || item.damage <= 0 || item.channel && Player.channel)
                return null;

            return true;
        }

        /// <summary>A hit landed: the copy's teaches it its weapon (MimicSystem.Landed); its owner's tells the copy what they are fighting.</summary>
        private void Hit(NPC target, int damageDone, int tag)
        {
            if (MimicSystem.Is(Player))
            {
                MimicSystem.Landed(target, damageDone, tag);
                return;
            }

            LastHit = target.whoAmI;
            LastHitAt = Main.GameUpdateCount;
        }

        /// <summary>The copy stood on the Empress's floor last tick (her walls hold it as they hold its owner).</summary>
        private bool walledFloor;

        /// <summary>
        /// The copy held inside the Empress's arena as its owner is: her arena holds the player at the keyboard alone
        /// (EmpressArenaPlayer), and the copy flies in there on its owner's favour (MimicSystem.Buffs). While her walls
        /// are up and it is inside them, out of her hand, nothing of its speed carries it through an edge, and it stands
        /// on her floor (Decagon.HoldInside, as for its owner).
        /// </summary>
        public override void PreUpdateMovement()
        {
            if (!MimicSystem.Is(Player))
                return;

            if (Player.dead || !EmpressArena.Up || !EmpressArena.Holds || Player.GetModPlayer<EmpressHeld>().Held
                || !Decagon.Inside(Player.Center, EmpressArena.Centre, EmpressArena.Size))
            {
                walledFloor = false;
                return;
            }

            Player.velocity = Decagon.HoldInside(Player.Center, Player.Size * 0.5f, Player.velocity, EmpressArena.Centre, EmpressArena.Size, walledFloor, out walledFloor);
        }

        public override void PostUpdateMiscEffects()
        {
            if (!MimicSystem.Is(Player))
                return;

            int extra = MimicSystem.Extra;
            Player.statLifeMax2 = (int)(Player.statLifeMax2 * (LifeTimes + LifePerSlot * extra));
            Player.statDefense *= DefenseTimes;
            Player.statDefense += DefensePerSlot * extra;
            Player.noFallDmg = true;

            if (Fresh)
            {
                Player.statLife = Player.statLifeMax2;
                Player.statMana = Player.statManaMax2;
                Fresh = false;
            }
        }

        /// <summary>
        /// The copy's weapons hit as summons do (by request: "all weapon damage also scale with summoner buff"): a weapon's
        /// damage with its own class's bonuses, as anyone's, then its owner's summon damage on top (what the copy wears
        /// and has is its owner's, so its summon bonus is theirs; only summon's own, not the generic bonus counted
        /// twice; not again for a summon weapon, which has it already), then a fifth more for each slot past twelve.
        /// </summary>
        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            if (!MimicSystem.Is(Player))
                return;

            float more = 1f + DamagePerSlot * MimicSystem.Extra;

            if (!item.CountsAsClass(DamageClass.Summon))
            {
                StatModifier summon = Player.GetDamage(DamageClass.Summon);
                more *= summon.Additive * summon.Multiplicative;
            }

            damage *= more;
        }

        public override void PostUpdate()
        {
            if (MimicSystem.Is(Player))
            {
                JumpSpeed = Terraria.Player.jumpSpeed;
                JumpTicks = Terraria.Player.jumpHeight;
                return;
            }

            if (Cloud > 0)
                Cloud--;
        }

        public override void FrameEffects()
        {
            if (MimicSystem.Is(Player))
                Player.armorEffectDrawShadow = true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (!MimicSystem.Is(Player))
                return true;

            Broken = true;
            playSound = false;
            genDust = false;
            return false;
        }
    }
}
