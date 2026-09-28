# PisAndDag — Item Plan

> Every item the mod adds, in three parts: **weapons**, **consumables**, **lore items**.
> Lore lives in `Terraria_Mod_Lore_Bible_ver_1.1.md`; this file is the machine.
>
> `build.txt` has no `modReferences`. Everything here is balanced against **vanilla** and
> only sanity-checked against Calamity. Nothing may depend on Calamity loading.
>
> Replaces `PAD_Greatbow_Plan.md`, which should be deleted.

---

# 1 · Weapons

`Content/Items/Melee`, `Content/Items/Ranged`

| item | class | state |
|---|---|---|
| Blade of Mors | Melee | built |
| Dented Megashark | Ranged | built |
| Chlorophyte Rifle | Ranged | built |
| Handswitch | Ranged | built |
| **Greatbow of a Pilgrim** | Ranged | **textures in, code to write** |

---

## Greatbow of a Pilgrim

Handed over by Pugream after Armageddon. Lore: Bible §XXXI *The Greatbow*, §IX *What He
Spends*. The player watched this weapon work, once, in somebody else's hands, before it
broke.

### Files

| | |
|---|---|
| item | `Content/Items/Ranged/GreatbowOfAPilgrim.cs` + `.png` ✔ |
| holdout | `Content/Projectiles/Ranged/GreatbowOfAPilgrimHoldout.cs` + `.png` ✔ 20×64 |
| string | `Content/Projectiles/Ranged/GreatbowOfAPilgrimString.png` ✔ 20×64 |
| ammo | `Content/Items/Ranged/GreatArrow.cs` + `.png` — §2 |

**One projectile, two layers.** A projectile type has exactly one
`TextureAssets.Projectile[type]`, so the string is **not** a second projectile. The holdout
draws both in `PreDraw`, requesting the string by path. One object, one netcode entity,
nothing to keep in sync. Both textures share the 20×64 canvas so they overlay at the same
origin.

### The string does the whole animation

**The stave never moves.** One shader, not two.

It used to bend forward, lose height and widen, all driven by the draw counter, and all of
it came out after watching Heavenly Gale — which never deforms its bow at all. A stave that
moves competes with the string for the eye, and the string is the part carrying the
information. Rigid wood reads as rigid wood, and the V in front of it does the talking.

So the animation is two straight runs from the nocks to a point that travels back as the
counter climbs, drawn between three positions in `PreDraw`. Nothing about it is a texture
warp, so the pull is limited by nothing.

`Effects/GreatbowString.fx` is all that is left, and it does what geometry cannot: pinches
the run toward the apex so the two meet in a spike rather than two ribbons crossing,
thins and brightens the whole run as the bow comes back, and puts a shimmer along it,
because the string is hair from an animal that does not live in this land.

Draw it in `PreDraw` with `spriteBatch.End()`, re-`Begin` in `SpriteSortMode.Immediate` with
the mod's own `MiscShaderData` applied, and restore the batch afterwards. The mod already
does custom `.fx` (`Effects/MorsCloak.fx`) — follow that loading pattern.

**`Effects/GreatbowLimb.fx` and its `.xnb` are dead files.** Delete them.

### The hands

`SetCompositeArmFront` / `SetCompositeArmBack`, the Heavenly Gale way. The back arm points
along the aim, a quarter turn off because the composite arm measures from straight down.
The front arm's angle falls out of the string: half of it is a right triangle, nock-to-centre
height against the pull, so the hand sits at `atan(half / pull)`. At rest the arm lies along
the bow; at full draw it is back by the ear. It tracks the counter for free, because it reads
the same two numbers the string is drawn from.

**Flip handling.** Aiming left, a plain half turn carries the belly round but stands the
stave on its head. Turn it **and** mirror it — `rotation += Pi` with `FlipHorizontally` when
`cos(rotation) < 0` — and it stays upright either way. The string is built from points, not
a sprite, so it uses the unflipped rotation.

### The draw

`Item.channel = true`, `Item.shoot` = the holdout, `CanShoot => false`. The holdout owns
the counter, the shaders, the meter and the release.

| held | damage | lodge hits | stagger | push back | flight |
|---|---|---|---|---|---|
| < 0.5 s | — | — | — | — | **cannot release** |
| 0.5 – 1 s | **350** (1.0×) | 1 | none | none | curved and flaccid; it droops |
| 1 – 3 s | 350 → **525** (→1.5×) | 5 | ≤ 1 s | none | like a Chlorophyte Shotbow |
| 3 – 5.5 s | 525 → **1050** (1.5→3×) | 7 | 2 – 4 s | **2 tiles** | flatter, still falls |
| 5.5 – 6 s | holds at 3× | 7 | 2 – 4 s | 2 tiles | — |
| ≥ 6 s | **1400** (4×) | 10 | **5 s** | **10 tiles** | dead straight, endless, fast |

`Item.damage = 350`. Everything above is a multiplier on it, computed at release and
carried into the projectile.

**The half second that does nothing.** 5.5 to 6 is a dead band: damage stops climbing, then
snaps to 4× on the sixth second. Keep it — it is the weapon refusing to be smooth — but it
must be **marked on the meter**, or it reads as a bug rather than a gate.

**The missing second.** Stagger at full draw is 5 s; the draw is 6 s. The player is always
one second short of being ready when the thing gets up. His bow drew in five. That gap is
the wound *and* the balance: a 5 s stagger on a 5 s draw is a permanent lock on every boss
in the game. Do not close it. (Bible §XXXI carries this.)

### The arrow

Same shape as `Content/Projectiles/Melee/BladeOfMorsProj.cs` — read that file first.

```csharp
Projectile.penetrate = -1;
Projectile.tileCollide = false;
Projectile.usesLocalNPCImmunity = true;
// set per shot, NOT a constant: cooldown = staggerTicks / hitsPerLodge
```

On hit: stash the velocity, zero it, run a lodge timer, resume — the `FreezeTimer` /
`storedVelocity` pattern. **The lodge lasts exactly as long as that tier's stagger**, so it
bites, hangs in the thing for the whole stagger, tears free and carries on to whatever is
behind. One object, two effects, nothing to explain.

Cooldown falls out of the tier:

| tier | stagger | hits | `localNPCHitCooldown` | per shot | DPS |
|---|---|---|---|---|---|
| 1 – 3 s | 60 t | 5 | 12 | 2 625 | ~875 |
| 3 – 5.5 s | 120–240 t | 7 | 17–34 | 7 350 | ~1 336 |
| ≥ 6 s | 300 t | 10 | 30 | **14 000** | **~2 333** |

Vanilla's best single-target ranged sits under a thousand, so the full draw is roughly
2.5× anything else in a vanilla world. That is the intended shape — a six-second
commitment with nothing else to do — but it is the number to cut first if the fights stop
being fights.

### Stagger

Terraria has no stagger. `GlobalNPC` with `InstancePerEntity`:

- a `StaggerTicks` field on the instance;
- `PreAI(NPC npc)` returns **false** while it is above zero, and zeroes `npc.velocity` in
  the same call;
- decrement in `PostAI` so it keeps ticking while AI is skipped.

`GlobalNPC.PreAI`, `AI`, `ModifyIncomingHit` and `OnHitByProjectile` are all verified
present in the staged `tModLoader.dll`.

**Risk.** Skipping AI breaks bosses whose state machines assume they run every tick —
teleporters mid-teleport, multi-segment worms, anything with a despawn gated on a counter
in `npc.ai[]`. Expect a **blacklist**. Test worms first.

### Push back

2 tiles (32 px) at the middle tier, 10 tiles (160 px) at full, **uniformly and lightly** —
the same distance whatever it is, delivered as a slide rather than a punt.

Do it by writing position directly. Not `knockBack`: every boss in the game resists that,
and "even the Wall of Flesh goes backwards" is the whole point of the line.

**The Wall of Flesh** drives its own X from its AI and accelerates as it loses health, so
the push has to survive the Wall re-deriving position on the next tick. Check that it
sticks instead of snapping, and that both eyes and The Hungry follow it.

### The skyward rain

Right-click. He points it up, **charges 8 seconds**, and 10–15 Great Arrows come down on a
point **locked before the charge starts**.

- The point is fixed at the moment of commitment. Everything lands there.
- Each arrow staggers and applies bleed.
- No cancel, no refund. Eight seconds is the cost and it must feel like one.
- Draw a marker on the locked point for the whole charge, so the player can watch the
  target walk out of the box they just paid for.

It costs about twelve shots of ammunition and returns about one shot of damage. **That is
correct.** The rain is a control tool, not a damage rotation. Anyone who later "fixes" its
damage has broken it.

### Open

- Dead-band marking on the meter (§ the draw).
- Whether repeated staggers on one NPC diminish — 5 / 3 / 2 s with a 1 s floor, resetting
  after ~15 s — or stay flat and this bow is the answer to every boss.
- Bleed: vanilla debuff or a PAD one.
- Where Great Arrows come from.

---

# 2 · Consumables

`Content/Items/Ranged` for ammunition; elsewhere by type.

| item | kind | state |
|---|---|---|
| **Great Arrow** | ammunition | to write |

## Great Arrow

The Greatbow takes nothing else, and nothing else takes Great Arrows.

- arrow: `Item.ammo = ModContent.ItemType<GreatArrow>()`, `Item.shoot` = the lodging
  projectile.
- bow: `Item.useAmmo = ModContent.ItemType<GreatArrow>()`.

One per draw, **ten to fifteen per rain**. They are the weapon's economy and the reason
the special attack is a decision rather than a rotation.

**[OPEN]** Source. Craftable, bought, or finite — and finite is the interesting one, given
what the weapon is.

---

# 3 · Lore items

`Content/Items/LoreItem`. `Item.PAD().Lore = true`, verses in `Item.PAD().LoreText`, `\n`
between them, shown on Shift. Voice rules in the `pad-lore-voice` skill: two to four
verses, ≤ 60 characters each, verse 1 what it is, verse 2 who held it, verse 3 the gap.

| item | state |
|---|---|
| Melee Decider | built |
| Pouch of Berries | built, sprite redraw outstanding |
| Lore Lock (system) | built |
| Game Changing Orb | built |

## Greatbow of a Pilgrim — text

The bow is a weapon, not a lore item, but it carries lore text. All proposals.

**Name.** *Greatbow of a Pilgrim.* Indefinite article. He is not named on it.

**Flavour** (≤ 55 chars):

> `'The string is hair. He did not say whose.'`

**Verses** (≤ 60 chars each):

> Rebuilt from a break, and it does not draw as it did.
> The man who carried it spent it on one animal.
> The wood is not from here.
> Nobody asked him and he is still walking.

---

# 4 · Magic — the memory system

**[BUILT. §4.1–4.6 are the design as argued, kept for the reasoning; §4.7 is what exists,
and wins where they differ.]**

Lore for all of it is §XXIV of the Bible, *Where magic comes from* — drawn out of the
land, not given by gods.

## 4.1 The shape

Spells and incantations are **not held items**. They are memorised, and cast through a
catalyst held in the hand.

| | |
|---|---|
| **Memory** | one shared pool. Starts at 2, raised by a consumable, hard cap **10**. |
| **What fills it** | spells AND incantations, from the same pool. |
| **Catalysts** | a **staff** casts spells. A **seal** casts incantations. |
| **Casting** | left click, with the catalyst in hand. |
| **Selecting** | two keybinds, up and down. |

**Cycling skips what the held catalyst cannot cast.** Holding a staff, up and down move
between the memorised *spells* only; the incantations stay memorised and keep their slots
but are not in this cycle. This is the whole reason there is no "selected an incantation
while holding a staff" case — it cannot arise, and a cast that does nothing is the worst
feedback a fight can give. The selection is remembered **per catalyst**, so staff → seal →
staff returns to the spell that was up.

**The selection UI only appears while a catalyst is held.** A full memory screen lives at
the table below.

## 4.2 Why one pool, and why it must cost something to change

One pool is what makes a build. Two pools of ten would mean everyone fills both and
nothing is chosen; one pool of ten means six spells and four incantations is a decision,
and ten incantations is a different character.

**Memorising happens at a placed tile, not anywhere.** If the loadout can be rearranged
mid-cave for whatever is in the room, the cap constrains nothing and the system is an
inventory with extra steps. The friction is the design.

**Ten slots wants twenty to thirty castables** before the cap bites. Below about twice the
cap, everyone memorises nearly everything and the choice is decoration. Content
commitment, noted here so it is a decision rather than a surprise.

## 4.3 Spell or incantation

Same source — there is no divine half, see §XXIV — so the split is **how it is drawn**,
not where from.

- A **staff** is an instrument. The Arcanum's engineering: focused, projected, a thing
  that leaves the caster and travels.
- A **seal** asks the ground directly. Slower, wider, comes out of the world rather than
  out of the hand. Terrain, zones, growths.

Both drink the same gas.

## 4.4 Cost

Everything costs mana. Some cost something else on top, and **life is the mod's existing
vocabulary for that** — the Handswitch already trades lifeforce for powerful shots.

**On vanilla mana.** The wish to make regeneration less generous is fair, but a global
change rewrites every vanilla magic weapon for anyone with this mod installed, and someone
playing Demon Scythe in a PAD world did not ask for that. Mana Sickness already exists as
the anti-spam lever, and Calamity notably does not touch regen either — it adds expensive
weapons and its own resources instead.

Preferred order:

1. Make PAD's own casts expensive. A forty-mana spell self-limits and touches nothing.
2. Secondary costs, as above.
3. If the global change is still wanted, it is **a config toggle, default off** — a
   difficulty option rather than an imposition.

## 4.5 Type and element

Two labels on every magic weapon, spell and incantation, in the tooltip.

- **Type** is what it does: explosion, heal, pierce, and so on.
- **Element** is where it was drawn from: blood, frost, flame, wind, lightning,
  chlorophyte, decay, hallow, and whatever else the world has enough of.

They are **labels, not a grid**. Nothing requires every type-element pair to exist.

**Elements are regions**, because the power comes out of the land (§XXIV). The list looks
unrelated because biomes are unrelated, and that is correct rather than a problem. Draw
elements from what the mod has actually built — blood from the Abomanation, chlorophyte
from the rifle and `ChlorophyteSpill`, lightning from `Lightning` and Rage of a Thunder —
so the taxonomy reads as this world's physics instead of a genre checklist.

**Ship it as tooltip text first.** As labels it costs nothing and teaches a vocabulary.
The moment it is mechanical — resistances, weaknesses — every enemy in vanilla needs
resistance data, which is an enormous table and a permanent maintenance burden. Design the
labels so they *can* become mechanical later; that addition is additive, and the reverse
is a rewrite.

**[OPEN]** Whether an element cast on the ground it came from behaves differently, and
whether an element can only be learned in its own biome.

## 4.6 What has to be built, in order

1. `ModPlayer` memory: the list, the cap, save, load, and sync.
2. The two catalyst base classes, staff and seal.
3. The two keybinds and the per-catalyst remembered selection.
4. The cycle HUD, shown only while a catalyst is held. `MagazineHUD` is the nearest
   existing shape to copy.
5. The memory tile and its full UI.
6. The memory consumable, capped at 10.
7. Then, and only then, spells.

## 4.7 What is built

**This section is the state of the code. Where it and §4.1–4.6 disagree, this wins.**

### The machine

| Part | File | What it does |
|---|---|---|
| Magic Arsenal | `Content/PADplayer/MagicArsenal.cs` | `ModPlayer`. One pool of `MaxSlots` 10, `StartSlots` 3 open. `Unlocked` (earned), the slots (air included, so a cast keeps its place) and the selection per kind are saved. No two of the same cast. Slots take or give only within `HomeReach` (800 px, 50 tiles) of a town NPC's home tile. |
| Arsenal window | `Content/UI/MagicArsenalUI.cs` | Ten slots beside the inventory, shut ones banded. Shares a corner with the Lore Codex; opening one closes the other. Right click a cast to select it; the selected one per kind wears the ember frame. Away from a house the slots show dimmed and a click says why. |
| `Castable` | `Content/Items/Mage/Castable.cs` | Abstract `ModItem` for every spell and incantation. `Kind`, `CastDamage`, `Mana`, `UseTime`, `Shot`, `CastType`, `Element`; virtual `Channel`, `Speed`, `Knockback`, `ReuseDelay`, `CanCast`, `Cast`. Does nothing in the hand. |
| `Catalyst` | `Content/Items/Mage/Catalyst.cs` | Abstract staff or seal. No numbers of its own: every tick it copies damage, mana, use time, reuse delay, channel and shot from the selected cast of its kind, so vanilla's mana, timing, tooltip and prefixes work on it. Nothing selected, `CanUseItem` is false. |
| Keybinds | `PADKeybinds.SpellUp` (Up), `SpellDown` (Down) | Only while a catalyst is held, and only through casts of its kind. |
| Colours | `Content/Items/Mage/MagicColours.cs` | Fill and stroke per kind and per element; see below. |
| Makeshift Altar | `Content/Tiles/MakeshiftAltar.cs`, item in `Content/Items/Misc/` | Crafting station, 3×2, where casts are written. Nothing else. |
| `/magicarsenal` | `MagicArsenalCommand.cs` | **Test only** (RELEASE_TODO). Bare: reports open and earned. `max`: all ten open (`Maxed`, saved). `normal`: back to `Unlocked`; a cast in a slot that shuts moves to the first empty open slot, or is given back to the player. `Unlocked` is never touched, so going back is exact. |

**Where it left the design:**

- Starts at **3** slots, not 2.
- Memorising happens **near a lived-in house**, not at a memory tile. The Makeshift Altar
  is only where casts are crafted.
- **Nothing raises `Unlocked` yet.** The slot consumable (§4.6 step 6) is not built; only
  `/magicarsenal max` opens more.
- The selection is remembered per kind (spell, incantation), which with two catalyst kinds
  is the same as per catalyst.

### Catalysts

| Catalyst | Class | Kind | Source |
|---|---|---|---|
| Branch Staff | `Staves/TreeBranchStaff` | Spell | Dryad's shop |
| Clothier's Chatelaine | `Seals/SealOfAClothier` | Incantation | Clothier's shop; Skeletron (classic drop, bag in expert and master) |

The only seal comes after Skeletron, so no incantation can be cast before him.

### Casts

| Cast | Class | Kind | Type | Element | Damage | Mana | Use time | Rarity | Source |
|---|---|---|---|---|---|---|---|---|---|
| Cold Call | `Spell/ColdCall` | Spell | Evocation | Frost | 36 | 14 | 25, then 15 reuse delay | Green | Altar: 30 Ice Block, Book, 5 Sapphire |
| Aquatic Firearms | `Spell/AquaticFirearms` | Spell | Conjuration | Conquest | 92 (the S.D.M.G.'s shot) | 50, then 30/s after 3 s | 20, channel | Red | **none yet** |
| Meagre Flame | `Incantation/MeagreFlame` | Incantation | Evocation | Flame | 40 | 18 | 60, channel | Green | Fire Imp, 1 in 20 |
| Court of Hands | `Incantation/CourtOfHands` | Incantation | Conjuration | Blood | 30 | 18 | 45, channel | Green | Altar: 20 Essence of the Fouled, Book |
| Everhunger | `Incantation/EverHunger` | Incantation | Conjuration | Blood | 51 | 18 | 30 | Green | Altar: 29 Essence of the Fouled, Book |
| Forest, Nurture Me | `Incantation/ForestNurtureMe` | Incantation | Abjuration | Flora and Fauna | 0 | 25 | 30 | Blue | Dryad's shop |
| Chlorophyte, Arm Me | `Incantation/ChlorophyteArmMe` | Incantation | Transmutation | Flora and Fauna | 0 | 30 | 30 | Lime | Witch Doctor's shop, after Plantera |

All under `Content/Items/Mage/`. Seven casts against ten slots: §4.2 wants twenty to thirty.

### Element colours (`MagicColours`)

| Element | Fill | Stroke |
|---|---|---|
| Flame | `#792620` | `#d77f2d` |
| Blood | `#ff4242` | `#680303` |
| Frost | `#9fe2ff` | `#1d3b7a` |
| Flora and Fauna | `#0f3311` | `#1d9f49` |
| Conquest | `#7a3a24` | ramp, bottom to top: `#bdab51` → `#c8a55b` → `#dbd88f` |

Kind words: Spell `#38462b` on `#da7210`; Incantation `#d65a51` on `#5d7ba2`.

**A ramp stroke** (`StrokeRamps`, `RampOf`) cannot come from vanilla's shadow pass, which
draws a glyph in one colour. `Stroked(..., ramp)` lays the text out word by word exactly as
`ChatManager` does (the same kerning and spacing through `DynamicSpriteFont`'s public
`SpriteCharacters`), draws the four outline offsets at spread 2 a glyph row at a time, each
row in its colour along the ramp, then the fill over it with `ChatManager.WaveColor`. Plain
text only, no colour codes. `Castable` and `Catalyst` pass the ramp to the element and
"Up:" lines. Add an element with a ramp by adding one entry to each dictionary.

### Aquatic Firearms

Held, a Minishark, Megashark and S.D.M.G. hang over the caster, strongest on top (offsets
0,-38 / 0,-63 / 0,-92), turn to the cursor and fire musket balls.

- **Numbers are read off the real guns** (`AquaticFirearmsBattery.Stats`, from
  `new Item(type)`, so PSItem's use times 6 / 6 / 4 are in): use time, speed, knockback,
  crit, sound, and vanilla's scatter (±0.4 per axis, ±0.2 for the S.D.M.G.).
- **Shot damage is gun + musket ball, rounded:** 13 / 32 / 92 before magic bonuses.
  *(Designed as ×2/3; changed to full by the user. The doc comments in
  `AquaticFirearmsBattery.cs` still say ×2/3.)*
- No ammo. The bullets are `ProjectileID.Bullet` from `GetSource_FromThis()`, set to
  magic damage and given the gun's crit after spawning.
- **Mana:** the 50 cast pays for `FreeTicks` 180; then `DrainPerSecond` 30, paid a point at
  a time. Let go, run dry, put the staff away or change spell, and they leave.
- **Look:** `AquaticFirearmsSkin` paints each gun's silhouette with a deep-sea nebula and
  stars drifting in two layers (`AquaticNebula.png` 256×64, `AquaticStars.png` 128×64),
  CPU-painted, no shader. Rim and halo are tinted by a glow that swings between two colours
  per gun (`GlowA` / `GlowB` in the battery). In: three Bezier ribbon waves out of the caster's
  back, each gun growing out of its wave (ease-out-back). Out: the same backwards, top
  gun first (ease-in-back). The wave sides are frozen at cast, so turning does not flip
  them.
- One-off sounds and bursts go through a `played` bitmask with a 6-tick late window, so a
  skipped or repeated tick neither loses nor doubles them.
- One battery at a time (`CanCast`: `ownedProjectileCounts == 0`). The tooltip's damage
  line shows all three shots.

### Chlorophyte, Arm Me

Puts on **Armed** (`Content/Buffs/Armed.cs`) for 20 s; will not recast while more than
5 s are left. `ArmMeCall` is the cast effect only (green motes drawn in, a bloom,
chlorophyte dust and jungle leaves).

- **The turning** is `ArmedRounds`, a hook on
  `On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float`
  (every overload ends there). A lesser bullet whose owner is Armed is made a
  `ChlorophyteBullet` instead, with its damage, speed and knockback as given: it homes and
  hits as hard as it would have, and loses its own effect (a Cursed Bullet's fire).
- **Only the player's own shots:** the source must be an `EntitySource_Parent` whose entity
  is the owner, or a non-hostile projectile of theirs. NPC bullets never turn. Covers
  Aquatic Firearms' guns.
- **Lesser** is worked out in `PostSetupContent`: a bullet projectile takes the lowest
  rarity among the ammo that **defines** it (vanilla ammo for a vanilla bullet, the same
  mod's ammo for a modded one) and is lesser below Lime. Luminite and Chlorophyte Bullets
  stay. The list is logged to `client.log` at load. Vanilla result: 12 kinds (14, 36, 89,
  104, 242, 279, 283, 284, 285, 286, 287, 981).
- **Why "defines":** LuiAFK Reborn's Unlimited ammo clones every vanilla bullet at rarity
  10. Letting all ammo vote made every kind rarer than chlorophyte and nothing turned.

---

# Conventions

- A texture sits beside its `.cs`, named identically; the path derives from the
  **namespace**, which mirrors the folder.
- Glow layers are a separate `…Glow.png` on transparency.
- No `.png` is needed where a projectile borrows a vanilla texture and returns false from
  `PreDraw`.
- Item sprites are single-frame unless registered in `Main.itemAnimations`.
