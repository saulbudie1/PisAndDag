# PisAndDag: pre-hardmode checklist

Asked for after the comparison with Calamity's pre-hardmode: "Create check list to rival
theirs". What a world before the Wall has in Calamity, what it has here, and what is still to
make, stage by stage, to stand beside it. It is the list to tick.

- `[x]` built, and a player can get it: where from was read in the mod's own code (session 7,
  PADWorkFlow §16.75).
- `[ ]` asked for or planned, not built: where it is written down.
- `[ ] (mine)` a proposal nobody has asked for. Keep it, change it, or strike it out.
- **Grand**: only in a Grand world, on the mod's own layout (the Wasted Mile, the Cleared
  Ground, Grace).

Calamity's numbers are its own items, read from Calamity 2.2.2's assembly (the Workshop copy),
and re-run in PADWorkFlow §16.77: what a player can have before the Wall of Flesh. Calamity
marks that with rarity, White to Orange, except that it gives its last boss before the Wall,
the Slime God, Light Red, the rarity of early hardmode. So: everything White to Orange; of the
Light Red, what the Slime God drops, what is made from what it drops (Purified Gel), and what
has another source before the Wall, each read where it comes from (recipes, loot, shops,
chests); and nothing whose only source needs hardmode (the Astral Infection's wood, fruit and
water) or that nothing gives. Weapons are items with damage and a damage class, or in its
weapon folders, that are not ammunition, tools or tiles; accessories leave out vanity; armour
is counted in sets; tools are its `Items.Tools` (pickaxes, axes, hammers and its gadgets);
potions are its `Items.Potions`, its food and drink among them. Each weapon and accessory is
named in section 6, and each of the mod's own in section 5.

---

## 1. Where it stands

| | Calamity 2.2.2 | PisAndDag now | to rival it |
|---|---|---|---|
| Bosses | 5: the Desert Scourge, Crabulon, the Hive Mind, the Perforators, the Slime God | 2: Amalgaermis, 'Dame' Quilt (both Grand) | 5 |
| Minibosses | 1: the Giant Clam | 0 | 1 or 2 |
| Events | 1: Acid Rain, its first tier | 0 | 1 |
| New places | 2 biomes: the Sulphurous Sea, the Sunken Sea | the Wasted Mile, the Cleared Ground and Grace (Grand); the Blood Shrine, the shoreline graveyard, the Mors monument | one in every world size |
| Weapons | 161: melee 32, ranged 30, magic 27, summon 27, rogue 43, two of no class | 16: melee 5, ranged 4, magic 4 casts (a fifth only shields; and 2 catalysts), summon 3 | 116: melee 32, ranged 30, magic 27, summon 27 (there is no rogue here) |
| Accessories | 72 (5 for expert) | 6 | 72 |
| Armour | 8 sets, 35 pieces | 0 | 8 |
| Tools and gadgets | 29 | 1, the Trumpet (and a crafting station, the Makeshift Altar) | 29 |
| Potions, food and drink | 24 | 0 | 24 |
| Materials | 14 | 1, Essence of the Fouled | 14 |
| Lore items | 14 | 3: the Piece of Paper, the Pouch of Berries, the Locket | as the story needs |
| Difficulty | Revengeance, Death and its other modes | Legends (the Game Changing Orb) | |

What there is is deep (every built thing has its own machinery, words and art) and thin: two
fights in five stages, four ranged weapons and three summon weapons, and nothing to wear, drink
or mine. Both bosses, what they give, and the Handswitch are in Grand worlds only. The targets
above are mine: Calamity's numbers, less its own class.

The mod's rules shape what fills the gaps: no wings before the Empress of Light
(`PADWorld.FlightUnlocked`, Lore Bible §XXIX), so ground movement is worth more here than
anywhere; the game's guns have magazines and reloads (`PSItem`); the parry exists (`Parry`,
`ParryPlayer`; the Iron Flow is still the only thing that parries); magic is cast from memory
through a catalyst (PAD_Item_Plan §4); and the name of the mod is a promise of pistols and
daggers.

---

## 2. Stage by stage

### 2.1 The first days (no boss down)

The world: spawn in forest, snow two hundred tiles east, desert and the evil west
(PAD_World_Plan §6).

**Bosses and enemies**

- [x] **Blind Tusker**: the desert, by day, in a sandstorm before the Eye, and under the
  desert, at half the Antlion's chance; 200 life, charges at 36 mph, leaps gaps and walls of 15.
- [ ] **Mors at the graveyard** after the first death, a wandering encounter (PAD_Lore_Planning
  §4.2).
- [ ] **White Death, the first meeting** (PAD_Lore_Planning §8; when is open): unwinnable,
  three shots to 1 life, Held, three casings on the ridge. The shell is built; the meeting is
  not.
- [ ] (mine) **An early boss to stand with the Desert Scourge and Crabulon**: a Gemmason
  construct in a quarry ruin underground (Lore Bible §XXIII: quarries, gem inlays, old pylon
  networks; "Distance is only geometry"). It moves through its own pylons. After the Eye in
  the Boss Checklist. Drops gem catalysts and a short ground dash.
- [ ] (mine) **A miniboss**: an old bull among the Blind Tuskers, in sandstorms; bigger, slower
  to turn, its Iron Flow certain. Calamity's Giant Clam is its pair.
- [ ] (mine) Two or three more enemies of the country round spawn: the snow (the first danger
  east of spawn), the shoreline graveyard at night.

**Weapons**

- [x] Melee: **Fly Swatter** (3 Iron or Lead Bars, 2 Cobwebs, an anvil).
- [x] Melee: **Iron Flow**, the Tusked Man's Curved Sword (the Blind Tusker, 1 in 4).
- [x] Ranged: **Wooden Crossbow** (15 Wood, an Iron or Lead Bar, a Work Bench).
- [x] Ranged: **Mycellaneous** (50 Glowing Mushrooms, a Wooden Bow).
- [x] Ranged: **Handswitch** (Grand): always the first thing in the chest in the lookout of the
  Curtain's watch tower, in Grace; 80 a shot, ten to a magazine, and every shot paid in life.
- [x] Summon: **Hoard of Undead** (any zombie, 1 in 50).
- [x] Summon: **Worminator** (the Giant Worm, 1 in 5): a worm that goes through the ground
  after what you fight, a segment longer for each summon.
- [x] Catalyst: **Seal of a Clothier** (the Guide's Witchcraft kit, in a new world; later the
  Clothier and Skeletron).
- [ ] (mine) **Pistols and daggers in every ore.** A pistol (the game's magazines and
  reloads) and a dagger (parries, as the Iron Flow does) for each pair of ores: copper or tin,
  iron or lead, silver or tungsten, gold or platinum, and later demonite or crimtane,
  meteorite, hellstone. One recipe each, through "Any" groups as the Trumpet's. Seven of each:
  fourteen of the hundred, and what the mod is called.
- [ ] (mine) **An incantation for the first days.** The Witchcraft kit's seal has nothing to
  cast before the Dryad's shield or a Fire Imp's Meagre Flame.

**Accessories**

- [x] **Boots** (10 Silk, 5 Leather, 2 Iron or Lead Bars).
- [x] **Metallurgist's Brilliant Stone** (the Merchant).
- [ ] (mine) Ground movement, since nothing flies: claws that hold a wall, a short dash, runners
  for sand and snow. (The Triple Fart In A Horse Shoe Adorned Bottle is built; its recipe is
  still commented out.)
- [ ] (mine) A buckler that widens the parry.

**Armour, tools, consumables, materials**

- [ ] (mine) **A hide set from the Blind Tusker**: its hide and ivory; a set bonus that pays
  for running.
- [ ] (mine) **A gunsmith's set** (iron or lead, leather): reloads and magazines.
- [x] Tool: the **Trumpet** (3 Gold or Platinum, 6 Iron or Lead, 14 Copper or Tin Bars, an
  anvil).
- [ ] (mine) Gadgets: a cleaning kit for guns (faster reloads); a mason's level that rings near
  a buried Gemmason ruin.
- [ ] (mine) Tonics: a reload tonic, a steadying draught (no knockback, slower), sand-runner's
  oil.
- [ ] (mine) Materials: tusker hide and tusker ivory (the Blind Tusker).

### 2.2 After the Eye of Cthulhu

**Bosses and encounters**

- [x] **'Dame' Quilt** (Grand): on the Cleared Ground, once Pugream has seen off the Eye, before
  hardmode; 1,500 life. Drops the **Locket of a Book-Crazed Girl** (an accessory, and lore).

**Weapons and accessories**

- [x] Melee: **Visus** (the Eye, every time; in its bag in expert).
- [x] Cast: **Forest, Nurture Me** (it shields; it hurts nothing); catalyst: **Tree Branch
  Staff** (the Dryad's shop; she comes once any boss is down).
- [x] Accessory: **Dryad's Blessing** (the Dryad, for a Bezoar, once for each player).
- [ ] (mine) The early boss's drops (2.1): two gem catalysts, a cast of stone, the dash.

**World**

- [ ] **The defaced angel statue** in the graveyard, the Low Court's mark (PAD_Lore_Planning
  §4.2, Lore Bible §XVII).
- [ ] (mine) **The courier**: dead on a road, with a sealed letter in a script the player
  cannot read (Lore Bible §XVII, "How the Player Meets Them"). A lore item.

### 2.3 After the Eater of Worlds or the Brain of Cthulhu, and the Jungle

- [ ] **The Jungle apparatus ruin** (PAD_Lore_Planning §4.3; Lore Bible §XVII: the Low Court
  poisoned the Jungle with pumps, vats and pipes, "an industry run for as long as it took").
  (mine) Make it the place of the next three:
  - [ ] (mine) **Boss: the works, still running.** Machinery, not a monster ("metaphysical
    forces do not leave machinery"). Before Skeletron in the Boss Checklist.
  - [ ] (mine) **Event: the overflow.** After heavy rain the old vats spill into the Jungle:
    poisoned water and what lives in it, a bar to fill, the boss at the end. Calamity's Acid
    Rain is its pair.
  - [ ] (mine) Poison weapons, an antidote line (the Bezoar, the Dryad's wound) and
    oilcloth armour for whoever goes in.
- [ ] (mine) Meteorite: its pistol and dagger (2.1), and a gun of the mod's own made of it.
- [ ] (mine) Demonite and crimtane: their pistol and dagger.

### 2.4 After Skeletron

**Bosses**

- [x] **Amalgaermis** (Grand): ten small animals let go on the Wasted Mile; after Skeletron in
  the Boss Checklist; 10,000, 12,000 and 15,000 life (half as much again in Legends). Drops
  **Essence of the Fouled** (20 to 25 a player) and **Joint Arms of the Defiled** (a whip, one
  a player).
- [ ] Its **Implanting** (PAD_NPC_Plan §2.4), planned.
- [ ] (mine) **The Goblin facility** (Lore Bible §XXI: "a place the player can stand inside";
  "Nothing in it is labelled"), after the Goblin Army; the tally-plate in it (§XXI's
  proposal), and the fifth boss: one of the machines.

**Weapons**

- [x] Cast: **Cold Call** (30 Ice Blocks, a Book, 5 Sapphires, at the Makeshift Altar).
- [x] Cast: **Court of Hands** (20 Essence of the Fouled, a Book) and **Everhunger** (29 Essence,
  a Book), at the Makeshift Altar.
- [x] Melee: **Flesh Maiden** (the Visus and 13 Essence of the Fouled, at a demon altar).
- [x] Ranged: **Meat Hook** (a Shotgun, 3 Gold Bars, 10 Chain and 10 Essence of the Fouled, an
  anvil): counted before the Wall as you count it, though the Arms Dealer sells the Shotgun
  only in hardmode (RELEASE_TODO).
- [x] Summon: **Joint Arms of the Defiled** (above).
- [x] Catalyst: **Seal of a Clothier** (the Clothier's shop, and Skeletron every time).
- [ ] Amalgaermis's designed drops (PAD_NPC_Plan §2.8): **Gaping Jaw** (melee), **a hand minion**
  (summon), **a bow of teeth** with its tooth arrows (ranged), **the number** (lore, the first
  kill), its trophy and mask.
- [ ] **Something that throws** (PAD_NPC_Plan §2.8, Open): a weapon that ragdolls enemies along
  its swing, in the boss's yellow.

**Accessories and the rest**

- [x] **Bone Casing** (Skeletron, 1 in 4; in its bag in expert).
- [x] **Necklace of the Academy** (the Dark Caster, 1 in 15).
- [x] Station: **Makeshift Altar** (15 Bones, 10 Wood, a Book, a Work Bench).
- [ ] (mine) A summoner's set of bone from the Dungeon, for the Hoard of Undead's line.

### 2.5 The Underworld, before the Wall

- [x] Melee: **Blade of Mors** (driven into the Mors monument at the bottom of the world; weak
  by design until Mors remembers, Lore Bible §XVI).
- [x] Cast: **Meagre Flame** (a Fire Imp, 1 in 20).
- [ ] (mine) Hellstone: its pistol and dagger.
- [ ] (mine) A fire cast and a fire whip from the Underworld's own, so each class has
  something there.

### 2.6 Grand worlds

- [x] The Wasted Mile, the Cleared Ground, Grace and the halls under it.
- [ ] (mine) Grace's armouries and treasuries, read as loot: chests with the kingdom's own
  arms, so a Grand world pays for its walk.
- [ ] (mine) One place of the mod's own in every world size, so that a world that is not Grand
  has more than the Blind Tusker.

---

## 3. Across every stage

**Armour: 8 sets to rival.** None built.

- [ ] (mine) Tusker hide (2.1); a gunsmith's (2.1); a Gemmason's, for mages (2.2); a duellist's,
  for daggers and the parry (2.2); oilcloth from the works (2.3); a summoner's of bone (2.4); one
  of Grace (2.6).

**Potions, food and drink: 24 to rival.** None built.

- [ ] (mine) Tonics for guns (reloads, magazines), for the parry (its window), for ground
  movement (speed, jump), antidotes (the overflow), and food and drink of the mod's own
  country.

**Materials: 14 to rival.** One built (Essence of the Fouled).

- [ ] (mine) Tusker hide, tusker ivory, Gemmason cut stone, a pylon shard, the works' corroded
  brass and their sludge, goblin rivets, Grace's gilt.

**Tools and gadgets: 29 to rival.** One built (the Trumpet).

- [ ] (mine) The cleaning kit, the mason's level (2.1); a lockpick for Grace's chests; a
  courier's satchel (the letters); the pickaxes, axes and hammers each new ore and boss gives.

**Lore items.** The Piece of Paper, the Pouch of Berries and the Locket built.

- [ ] The courier's letter (2.2), the number (2.4), what the works and the facility leave.

---

## 4. Tally

Update as boxes are ticked.

| | to rival | built | planned | proposed (mine) |
|---|---|---|---|---|
| Bosses | 5 | 2 | 0 | 3 |
| Minibosses | 1 or 2 | 0 | 0 | 1 |
| Events | 1 | 0 | 0 | 1 |
| Weapons | 116 | 16 | 4 (Gaping Jaw, the hand minion, the bow of teeth, the throw) | 14 pistols and daggers, and more |
| Accessories | 72 | 6 | 0 | a handful |
| Armour sets | 8 | 0 | 0 | 7 |
| Tools and gadgets | 29 | 1 | 0 | 4 |
| Potions, food and drink | 24 | 0 | 0 | 3 named |
| Materials | 14 | 1 | 0 | 8 |

---

## 5. The mod's own, by name

Read where each comes from in the mod's code (PADWorkFlow §16.77, §16.78). Counted in section
1, as a player can have them before the Wall:

- **Weapons (16).** Melee: Fly Swatter, Iron Flow, Visus, Flesh Maiden (Grand), Blade of Mors.
  Ranged: Wooden Crossbow, Mycellaneous, Handswitch (Grand), Meat Hook (Grand; as you count it,
  §16.78). Magic: Meagre Flame, Cold Call, Court of Hands (Grand), Everhunger (Grand); and
  Forest, Nurture Me, which only shields. Summon: Hoard of Undead, Worminator, Joint Arms of
  the Defiled (Grand). Catalysts: Seal of a Clothier, Tree Branch Staff.
- **Accessories (6).** Boots, Metallurgist's Brilliant Stone, Dryad's Blessing, Bone Casing,
  Necklace of the Academy, and the Locket of a Book-Crazed Girl (Grand; a lore item as well).

Built, and not counted:

- **After the Wall.** Dented Megashark (the Megashark, after Queen Slime), Chlorophyte Rifle
  (Chlorophyte Bars), Betsy's Flames (Betsy); and two casts that hurt nothing, Chlorophyte, Arm
  Me (after Plantera) and Rage! Sweet Fire (Souls of Fright).
- **Nothing gives them yet.** Weapons: Foigh's Rust Side Sword (Foigh comes only by `/foigh`),
  Bone Squad (its recipe is commented out, and needs hardmode things), the Greatbow of a
  Pilgrim, Aquatic Firearms, the Vulture Staff (no source asked for; one written out and left
  off). Accessories: the Triple Fart In A Horse Shoe Adorned Bottle (its recipe is commented
  out; all of it is had before the Wall), Rage of a Thunder. A lore item: the Reliquary Key.

---

## 6. Calamity's, by name

What section 1 counts, by the names Calamity gives them in English. † marks the Light Red ones
that come before the Wall: the Slime God's, what is made from Purified Gel, and the Spelunker's
Amulet (an anvil recipe, and the desert shrine's chest).

**Melee (32).** Acidwood Sword, Air Spinner, Amidias' Trident, Aorta, Ball O' Fugu, Basher,
Bladecrest Oathsword, Broken Biome Blade, Burnt Sienna, Depth Crusher, Driftwood Sword,
Fractured Ark †, Gelitic Blade †, Goldplume Spear, Monstrous Knives, Mycelial Claws, Old Lord
Claymore, P-GSS: Auger, Perfect Dark, Riptide, Sahara Slicers, Sausage Maker, Seashine Sword,
Smoking Comet, Tainted Blade, Teardrop Cleaver, The God's Gambit †, Urchin Mace, Vein Burster,
Wind Blade, Wulfrum Screwdriver, Yateveo Bloom.

**Ranged (30).** Acidwood Bow, Aquashard Shotgun, Archerfish, Barinade, Bullet-Filled Shotgun,
Crackshot Colt, Dragoon Drizzlefish, Driftwood Bow, Eviscerator, Firestorm Cannon, Flurrystorm
Cannon, Fungicide, Galeforce, Goobow †, Gunk Shot †, Lunarian Bow †, M1 Garand, Magna Cannon,
Opal Striker, Overloaded Blaster †, P-TSL: Short Circuit, Pumpler, Reed Blowgun, Shadethrower,
Shellshooter, Slagfire Douser, Spark Spreader, Storm Surge, Toxibow, Wulfrum Blunderbuss.

**Magic (27).** Abyss Shocker, Abyssal Tome †, Acid Gun, Aquamarine Staff, Black Anurian, Blood
Bath, Coral Spout, Eldritch Tome †, Flare Bolt, Frost Bolt, Hellwing Staff, Hyphae Rod, Icicle
Staff, Mana Rose, Night's Ray †, P-PLS: Pulse Pistol, Parasitic Scepter, Plasma Rod, Sandstream
Scepter, Shaderain Staff, Sky Glaze, Sparkling Empress, The Cauldron, Tradewinds, Veering Wind,
Waywasher, Wulfrum Prosthesis.

**Summon (27).** Belladonna Spirit Staff, Blighted Slime Staff †, Brittle Star Staff, Caustic
Croaker Staff, Cinder Blossom Staff, Cnidarian, Dank Staff, Deathstare Rod, Enchanted Conch,
Enchanted Knife Staff, Eye of Night, Flesh of Infidelity, Frost Blossom Staff, Harvest Staff,
Herring Staff, P-PMA: Aqueous Hunter Drone, Polyp Launcher, Puff Shroom, Rusty Beacon
Prototype, Scab Ripper, Slime Puppet Staff †, Squirrel Squire Staff, Staff of Necrosteocytes,
Stormjaw Staff, Sun Spirit Staff, Vile Feeder, Wulfrum Controller.

**Rogue (43).** Antlion Skewer, Ashen Stalactite, Bouncing Eyeball, Cinquedea, Contaminated
Bile, Crystalline, Enchanted Axe, Feather Knife, Fishbone Boomerang, Gel Dart †, Gilded Dagger,
Glaive, Gleaming Dagger, Hardened Honeycomb, Infernal Kris, Infested Clawmerang, Iron
Francisca, Kylie, Lead Tomahawk, Lemon 'nade, Lionfish, Metal Monstrosity, Meteor Fist,
Mycoroot, Nasty Cholla, P-LSR: Aerial Tracker, Pumpkaboom, Rot Ball, Sand Dollar, Scourge of
the Desert, Seafoam Bomb, Shinobi Blade, Sludge Splotch, Snap Clam, Spadefish, Spore Knife,
Throwing Brick, Tooth Ball, Turbulance, Urchin Stinger, Walking Cane, Web Ball, Wulfrum Knife.

**No class (2).** Aestheticus, Skynamite.

**Accessories (72, 5 for expert).** Acrobatic Bobber, Alluring Bait, Amidias' Pendant, Ancient
Fossil, Anechoic Plating, Archaic Powder, Batholith Bangle, Black Glass Band, Bloodstained
Glove, Bloody Worm Tooth (expert), Cleansing Jelly, Coin of Deceit, Counter Scarf, Craw
Carapace, Crown Jewel, Depths Charm, Enchanted Pearl, Feather Crown, Filthy Glove, Fish Stocks,
Frozen Cube, Fungal Clump (expert), Fungal Symbiote, Giant Pearl, Giant Shell, Gladiator's
Locket, Harpy Ring, Heart of Darkness, Honey Dew, Ilmeris' Spark, Ink Bomb, Iron Boots, IV Drip
on the Rocks, Jelly-Charged Battery †, Laudanum, Life Jelly, Luxor's Gift, Mana Polarizer †
(expert), Marnite Repulsion Shield, Mirage Mirror, Ocean Crest (expert), Old Die, Protolith
Bangle, Radiant Ooze †, Raider's Talisman, Rotten Brain (expert), Rotten Dogtooth, Rover Drive,
Sand Cloak, Scions Curio, Scuttler's Jewel, Sea Spirit Amulet, Shield of the Ocean, Silencing
Sheath, Skyline Wings, Spelunker's Amulet †, Spirit Glyph, Spring Stool, Stress Pills, Sunken
Sinker, Tesla's Amulet, The Bee, The Pointer, Trinket of Chi, Unholy Tonic, Unstable Granite
Core, Vicious Tonic, Vital Jelly, Voltaic Jelly, Wulfrum Acrobatics Pack, Wulfrum Battery,
Wulfrum Bobber.
