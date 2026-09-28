# PAD Workflow — Handoff

> **Purpose:** everything a fresh chat needs to continue this work without re-deriving it.
> **Read the "Verified vs Unverified" section before trusting any API name in here.**
>
> Nothing is ever deleted here — superseded text is marked in place, so the reasoning
> behind an abandoned decision stays readable.
>
> **Start here:** **§12.9 — build a local compiler before writing any code.** Then §12.12
> (the live TODO), §12.10 and §11.9 (verified API facts), and the version traps in §11.9
> (`WorldGen.Convert`'s fake radius, `BiomeConversionID` not existing in 1.4.4).
> For magic: `PAD_Item_Plan.md` §4.7 (what is built), then §13.5 (the live TODO) and §13.3.
>
> **Sessions: 0–10 = session 1, §11 = session 2, §12 = session 3, §13 = session 4.** Later wins.

---

## 0. Project identity

- **Mod:** `PisAndDag` — display name **"Pistols and Daggers"**
- **Target:** tModLoader **1.4.4** / net8.0
- **Author:** Thai Thanh ("Saulbudie")
- **Mod root on disk:**
  `C:\Users\Admin\Documents\My Games\Terraria\tModLoader\ModSources\PisAndDag`

**Standing style directive from the user:**
> "skill teach: minimalist words, make every response minimal but still informative so it burns less token"

Namespace must mirror folder path (texture paths derive from it).

---

## 1. Folders to connect in a fresh chat

Connect these before starting. Without them nothing below can be read or edited.

| Path | Why |
|---|---|
| `C:\Users\Admin\Documents\My Games\Terraria\tModLoader\ModSources\PisAndDag` | **The mod itself. Required.** |
| `C:\Users\Admin\Downloads\DK` | Terraria decompile — **see warning below** |
| `C:\Users\Admin\Downloads\CalamityModPublic-1.4.4\CalamityModPublic-1.4.4` | Real authored Calamity source (1.4.4). Note the **doubled folder name**. |
| `C:\Users\Admin\Documents\My Games\Terraria\tModLoader\ModReader\CalamityMod\CalamityMod` | Older decompiled Calamity (superseded by the above) |
| `C:\Users\Admin\Downloads\tModUnpacker\Split` | Older decompiles |
| `C:\Users\Admin\Downloads\tModUnpacker\VFXPlus` | Older decompiles |

### ⚠️ The `DK` decompile is **Terraria v1.4.5.8**, NOT 1.4.4

This has already caused real bugs. Anything found in `DK` must be checked for existence in 1.4.4 before use.

**CORRECTED IN SESSION 2.** The claim below this line used to read "it only contains 7
source files". That is wrong. `DK` holds **1652 `.cs` files** and is a complete decompile.
Everything session 1 listed as "not present" is in fact there:

```
Terraria/Collision.cs   Terraria/Utils.cs      Terraria/Chest.cs        Terraria/Entity.cs
Terraria/CombatText.cs  Terraria/Dust.cs       Terraria/Item.cs (959 KB)
Terraria/Main.cs (2.1 MB)  Terraria/WorldGen.cs (2.2 MB)
Terraria.ID/TileID.cs   Terraria.ID/WallID.cs  Terraria.ID/BuffID.cs
Terraria.ID/BiomeConversionID.cs               Terraria.DataStructures/PlayerDrawLayers.cs
```

So `DK` **can** answer almost anything — the only real caveat is the version. It is
1.4.5.8, so anything found there still has to be checked for existence in 1.4.4. Session 2
hit exactly that: `BiomeConversionID` is in `DK` and does **not** exist in 1.4.4, and
`WorldGen.Convert` grew two parameters between the versions.

Staging `WorldGen.cs` or `Item.cs` and grepping locally takes seconds and is almost always
faster than guessing. Do that first.

---

## 2. Working method (this is the part that keeps going wrong)

**There is no `device_bash` in this setup.** The only way to change a file is:

1. `device_stage_files` — pull the file from the device into `/mnt/user-data/uploads/...`
2. Edit **that staged copy** (or a copy of it in outputs)
3. `SendUserFile` — get a `file_uuid`
4. `device_commit_files` — write it back with the `file_uuid` and the `expectedMtimeMs` from stage

### Hard rules learned the hard way

- **ALWAYS stage the file from the device before editing it.** A workspace copy drifts from what the compiler actually sees. This caused a real build error (`BumpScreenShake` "already exists" — it didn't, on the device).
- **Never re-type a file's content from tool output** — output can be truncated.
- When the mtime guard trips, **diff before committing**, don't force.
- The user edits files between turns. Assume the device copy has changed.

---

## 3. What exists now — file by file

All paths relative to the mod root.

### 3.1 The `PAD()` accessor family — Calamity-style

**`Utilities.cs`** (root) — MODIFIED. Added alongside the pre-existing `PIS()`:

```csharp
public static PADPlayer PAD(this Player player)          => player.GetModPlayer<PADPlayer>();
public static PADGlobalNPC PAD(this NPC npc)             => npc.GetGlobalNPC<PADGlobalNPC>();
public static PADGlobalProjectile PAD(this Projectile p) => p.GetGlobalProjectile<PADGlobalProjectile>();
public static PADGlobalItem PAD(this Item item)          => item.GetGlobalItem<PADGlobalItem>();
```

Copied from **`CalamityMod/Utilities/GeneralExtensionUtils.cs`** (verified by reading it), which does:
```csharp
public static CalamityPlayer Calamity(this Player player) => player.GetModPlayer<CalamityPlayer>();
public static CalamityGlobalNPC Calamity(this NPC npc)    => npc.GetGlobalNPC<CalamityGlobalNPC>();
// etc.
```

Also **removed two junk usings** Visual Studio had auto-added to `GraveyardPass.cs`:
`using static System.Net.Mime.MediaTypeNames;` and `using static System.Runtime.InteropServices.JavaScript.JSType;`
(`MediaTypeNames.Text` can collide with things.) **Watch for these reappearing in other files.**

#### New files (all `InstancePerEntity => true`, deliberately empty data bags):
- `Content/NPCs/PADGlobalNPC.cs`
- `Content/Projectiles/PADGlobalProjectile.cs`
- `Content/Items/PADGlobalItem.cs` — **not** empty, see §3.3

#### Design note: why `PADPlayer` is separate from `PisPlayer`
`PisPlayer` owns camera / screen shake / flight gating — **state that persists across ticks**.
`PADPlayer` owns effect flags that are **wiped every tick in `ResetEffects`**.
Keeping them apart means the per-tick wipe can never kill a live camera pan.

---

### 3.2 Surging Core (Arc Surge–style lightning)

**`Content/PADplayer/PADPlayer.cs`** — NEW
**`Content/Projectiles/Ranged/SurgeArc.cs`** — NEW (contains two classes: `SurgeSource` and `SurgeArc`)

**Public API — one call, no bool flag:**
```csharp
player.PAD().SurgingCore(
    damage: 40, rate: 36, arcs: 1, range: 340f,
    thickness: 7f, throughWalls: false, colour: null);
```
Call it **every tick it should be active** (from `UpdateAccessory`, a set bonus, a buff's `Update`, a pet AI — anything running before `PostUpdateEquips`). Stop calling it and it's off.

**Stacking rules** (multiple callers in one tick merge, first-caller sets the terms):
| Param | Merge |
|---|---|
| damage | `Math.Max` |
| rate | `Math.Min` (faster wins) |
| arcs | `+=` (summed, capped at `MaxArcs = 8`) |
| range | `Math.Max` |
| thickness | `Math.Max` |
| throughWalls | `\|=` |
| colour | **first caller wins** (no sensible "best" of two colours) |

`surgeCooldown` is the only field that survives `ResetEffects` — it has to span ticks.

**What vanilla Arc Surge actually does** (learned from `DK`, cite lines):
- `Player.cs:49668` — the item fires proj **1122** at the cursor with **zero velocity**, then grabs up to **2 extra targets** via `GetSparkleGuitarTarget` (`Player.cs:47028`, a 1000×800 rect), gated by `Vector2.Dot(...) >= 0.5f` (~60° cone) and an 8px-step `Collision.CanHitLine` walk.
- `Projectile.cs:71895` — on first tick it builds a jagged path from `player.HandPosition` to itself and damages the **whole path** via `MultiPointHitbox`, `penetrate = -1`, then dies. **It never travels.**
- `Projectile.cs:40361` — `AI_203_StormLightning` is the shared lightning AI.

**Deliberate departures in our implementation:**
1. **Damage runs on the straight line**, not the drawn crackle. The crackle uses `Main.rand` per client, so hit-testing it would desync.
2. **`SegmentHitsBox` is hand-written** (slab method) instead of calling `Collision.CheckAABBvLineCollision` — that API could not be verified for 1.4.4 (`Collision.cs` is not in `DK`).
3. **Origin rides in `ai[0]/ai[1]`** so it's in the spawn packet and joining clients rebuild the same bolt.
4. **Colour/width/throughWalls ride on `SurgeSource : EntitySource_Parent`**, read in `OnSpawn`, synced via `SendExtraAI`.
   - *Why:* a `Color` is 32 packed bytes; a float `ai` slot either rounds off a channel or lands on a **NaN bit pattern** (A=255 → exponent all 1s).
   - *Why the source, not post-assignment:* `NewProjectile` **sends the sync packet before it returns**. Set fields after and every remote client sees a default-red arc for a frame.
5. **The visual spawns on the first `AI()` tick, NOT in `OnSpawn`.** A projectile arriving from the network is built field-by-field from the packet (extra AI included) and only then updates. Drawing in `OnSpawn` gives remote clients the default colour/thickness.

Uses the existing `Content/Particles/Lightning.cs` — `Lightning.Spawn(start, end, colour, width, lifetime, forks)`.
Debuff: `BuffID.Electrified` for 4–7s (vanilla's own numbers).
Texture: borrows `"Terraria/Images/Projectile_" + ProjectileID.Bullet`, `PreDraw` returns false — no PNG needed.

---

### 3.3 Lore items

**`Content/Items/PADGlobalItem.cs`** — the flag + tooltips
**`Content/Items/LoreItem/LoreLock.cs`** — the enforcement (a `ModPlayer`)

**Usage — one line in any `ModItem.SetDefaults`:**
```csharp
Item.PAD().Lore = true;
Item.PAD().LoreText = "\"It does not cause death. It completes it\"";
```
**No `SaveData`, no `NetSend` needed.** Every item is built through `SetDefaults` — including one loaded from a world file or arriving over the network — so the flag rebuilds itself wherever the item exists. Persisting it would create a second source of truth.

`LoreText` splits on `\n` into separate tooltip lines. Leave it null and shift does nothing.

#### Protections, and which mechanism does what

| Mechanism | Blocks |
|---|---|
| `UpdateInventory` forces `item.favorited = true` | quick stack, drop key, shift-click→trash, shift-click→shop (**4 vanilla paths for 1 line**) — and the player can't un-star it |
| `LoreLock.RescueTrash()` | hand-dragging into the trash slot |
| `LoreLock.RescueContainer()` | hand-dragging into a chest / piggy bank / safe / forge / void vault |
| `LoreLock.PurgeDuplicates()` | two of the same lore item in the inventory (lowest slot survives) |
| `PADGlobalItem.PrefixChance` → `false` | **all** modifiers |

**Why "undo" instead of "refuse":** tModLoader has no "this item may not enter this slot" hook. The alternative is detouring several `ItemSlot` overloads that every other mod also wants. So the item lands and is pulled back the same tick, before a frame is drawn with it there.

**`PrefixChance` is one hook covering everything** because of what `pre` means:
- `-1` = rolled on craft / buy / drop / chest
- `-2` = reforge at the Goblin Tinkerer
- `-3` = the question the tinkerer's UI asks before it will **accept the item into the slot**

```csharp
public override bool? PrefixChance(Item item, int pre, UnifiedRandom rand) => Lore ? false : null;
```
Signature **verified** against tModLoader's own `ExampleMod/Items/PrefixChanceGlobalItem.cs`.

#### Tooltips
- Bottom line always: **"Lore Item"** in `(237, 130, 24)`
- Above it, only when shift is UP *and* `LoreText` is set: **"Hold Shift for lore"** in grey `(150, 150, 150)`
- Shift held: `tooltips.RemoveAll(line => line.Name != "ItemName")` then lore in `(196, 180, 152)`

Shift is read as `Main.keyState.IsKeyDown(Keys.LeftShift/RightShift)` — **deliberately not** a helper like `PressingShift()`, which couldn't be verified.

#### Known gaps (told to user, not fixed)
- **Manual drag onto a shop slot sells it outright** — nothing to undo. Workaround: give lore items `Item.value = 0`; vanilla refuses to buy a worthless item.
- Ground drops and lava are not covered. `CanBurnInLava` / `CanResearch` were **deliberately skipped** — signatures unverified for 1.4.4.
- Codex and inventory **don't share** the uniqueness check: one on the shelf + one in your pack is currently allowed.

#### Items already flagged
- `Content/Items/Melee/BladeOfMors.cs` — MODIFIED, `Lore = true` + `LoreText` added in `SetDefaults`.
  Note this file's own `ModifyTooltips` gates its DeathMeter lines on `PADWorld.Legends`.

---

### 3.4 Lore Codex UI

**`Content/UI/LoreCodex.cs`** — NEW (a `ModSystem`)
**`Content/UI/LoreCodexButton.png`** — NEW, 64×32, two 32×32 frames (normal | hover)
**Storage** lives on `LoreLock` as `public readonly List<Item> Codex` with `SaveData`/`LoadData` via `ItemIO`.

⚠️ The button PNG is a **placeholder I generated with PIL** (a tome glyph). Redraw it freely — the code only cares about the 2-frame-wide layout.

**Button position is NOT guessed.** From `DK/Terraria/Main.cs`:
- `DrawBestiaryIcon` (line 43195) → x = **498**
- `DrawEmoteBubblesButton` (line 43141) → x = **534**
- both at y = `244 + 30 + 4` = **278**, size **30×30**, spaced **36** apart
- both shift by `+5 x / +168 y` when a chest/shop is open, `+24 y` when `editChest`

Ours continues the row at **x = 570** and copies the same shifts.

**Hover glow** is a frame swap (`texture.Frame(2, 1, hover ? 1 : 0)`, then `Width -= 2; Height -= 2`) — identical mechanism to the vanilla buttons.

**Window:** 4 columns, always one empty slot past the last stored item. Grows a row when that spare would fall off the end; caps at 4 rows, then mouse wheel scrolls and an orange thumb appears. Anchored at `WindowX = 498, WindowY = 320`, slot scale `0.85f`.

**Closing:** `if (!Main.playerInventory || ChestOrShopOpen) Open = false;` — Escape closes the inventory and the codex goes with it, so there's no second state to desync.

**Drawn immediately, not as a `UIState`** — it's a button and a grid of `ItemSlot.Draw` calls; the framework would mostly wrap what `ItemSlot` already does free.

Wheel is read as `Mouse.GetState().ScrollWheelValue` with a stored `lastWheel` — deliberately **not** `PlayerInput.ScrollWheelDeltaForUI`, which couldn't be verified.

---

### 3.5 Shoreline Graveyard worldgen

**`Content/Structures/GraveyardPass.cs`** — NEW, then iterated ~10 rounds against screenshots. The user edits constants between rounds — **always re-stage before editing.**
**`Content/Structures/PADStructureSystem.cs`** — MODIFIED, registers the pass:

```csharp
tasks.Insert(index + 1, new BloodShrinePass("PisAndDag: Blood Shrine", 100.0));
tasks.Insert(index + 2, new GraveyardPass("PisAndDag: Shoreline Graveyard", 80.0));
```
`index` = `tasks.FindIndex(p => p.Name.Equals("Micro Biomes"))`, falling back to `"Living Trees"`.

#### Pipeline (order matters, each step depends on the last)

```
ApplyPass → TrySide(left/right) → [both fail] → Force()
              ↓ best-scoring plot
            ResolvePalette(dominant surface tile)
            Shave:
              FellLivingTrees      ← whole trees removed BEFORE anything is built
              GenerateHillProfile  → heights[] + plantable[]
              per-column clear + fill + cap
              BlendSkirt ×2        ← ramps each foot out to meet real ground
              SquareTileFrame sweep (covers skirts)
              PlaceAngel           ← one statue, crown, BEFORE graves
              Plant                ← records lastStoneMin/Max
              BuildFence           ← span derived from those
```

#### 🔍 Logging — use this before theorising

`[Graveyard]`-prefixed lines via `ModContent.GetInstance<PisAndDag>()?.Logger.Info(...)`.

**Log location (Steam install — NOT under Documents):**
```
C:\Program Files (x86)\Steam\steamapps\common\tModLoader\tModLoader-Logs\client.log
```
Logged: pass start; per side the beach vars, span, `considered`/`surveyed`, `bestX`/`bestLevel`/`inland`/`bestScore`/`bestMaterial`; `BUILT` with fill/cap/stones; `FORCING`/`FORCED`.

**This turned three rounds of guessing into one run. Do it first.**

---

#### The shape — four wrong versions before the right one

This is the most instructive part of the file. Each attempt fixed the last one's bug and introduced its own.

| # | Approach | What broke |
|---|---|---|
| 1 | Rolling noise around the **average** ground | Could carve a dip instead of a rise |
| 2 | Terraces measured up from the **lowest** ground | I claimed this "guarantees clearing the surrounding land" — only true if rise > the plot's slope range. When it wasn't, the hill sat BELOW its neighbours and `HeadRoom` excavated the higher ground above it → **a crater** |
| 3 | Smooth cosine dome, lerp toward `crownY = highest − rise` | `highest` is the **highest point anywhere in the plot**. Clip one natural hilltop and every column gets dragged up to it → **a 30-tile mountain from a 5-tile request** |
| 4 | Additive lift: `heights[i] = ground[i] − lift·shape[i]` | Bounded correctly, but the hill **inherited every notch** of the ground under it → **battlements** |
| ✅ 5 | **Arc on the terrain's trend line** | current |

**The one that works:**

```csharp
float leftGround  = AverageGround(ground, 0, FootSample);
float rightGround = AverageGround(ground, PlotWidth - FootSample, PlotWidth);
float baseline    = leftGround + (rightGround - leftGround) * along;

int raised = (int)Math.Round(baseline - crownLift * shape);
heights[i] = Math.Min(raised, ground[i]);     // never dig
```

Take the ground at the two **feet**, run a straight line between them, build the arc on that. The line meets real ground at both ends so it blends exactly; between them it is perfectly smooth so none of the old surface shows through. `Math.Min` means where ground already stood higher than the arc it is left untouched.

`shape` is Mors's `BuildShape()`: `FlatFraction` of the half-width is dead flat, then falloff that is `DomeWeight` raised cosine + the rest straight ramp. Two clamp passes (one from each end) cap any step at 1 tile — **each only ever RAISES** a column its neighbour outran, because lowering could dig below ground again.

#### `BlendSkirt` — the user's own rule

The baseline comes from the ground at the *plot edges*, but ground a few tiles further out can be well below that, leaving a ledge. After the hill is built, walk outward from each foot:

```csharp
if (ground <= y)                  return n;   // land rising, nothing to blend
if (ground - y <= BlendTolerance) return n;   // ≤2 tiles, reads as terrain
y++;                                          // one tile down, keep going
ShapeColumn(x, y, level);
```

`ShapeColumn` was factored out of `Shave` so the skirt lays ground **identical** to the hill's rather than a second near-copy. `MaxSkirt = 48` stops a graveyard on a ravine lip walking a ramp across the world. Both calls return their length so the framing sweep covers them.

#### `FellLivingTrees`

Building through a living tree removed the middle and left the canopy and lower trunk floating with a wedge of hill through them. A living tree is one connected mass, so a flood fill from any tile inside the plot takes the **whole** thing.

- Coordinates packed as `y * maxTilesX + x` → frontier is a plain `Stack<int>`, no struct, no extra using
- Clears the hollow trunk's wall too — **`244`**, taken from vanilla's own comparison at `WorldGen.cs:15822`, not a `WallID` name
- `LivingTreeReach = 60` above the hill for the search; it only needs to find one tile
- `LivingTreeCap = 40000` so a fill can't run away

#### Siting — nothing rejects on biome

`considered == surveyed` in the log: every plot is buildable, the **score** picks. Terms: `1000` base, `−12`/tile of slope, `−25`/wet column, `−40`/unreadable column, `+150` for dirt-or-grass, and **`−BeachPull`/tile inland** — that last one was missing at first and the pass happily built a perfect graveyard 1,171 tiles from the water.

- **Unreadable columns are bridged, not fatal.** One column over a cavern used to reject the whole 64-column plot; Large coastlines have plenty, which is why Large failed disproportionately. Fails only below `MinReadableColumns`.
- **`Force(left)`** builds at a fixed inland offset if both shores somehow find nothing. **The pass cannot silently produce nothing.**
- **`Grounded(x, y)`** rejects floating islands: ≥`GroundSolidMin` of `GroundProbe` tiles below must be solid, else skip `IslandSkip` rows.
- **`ResolvePalette`** picks fill/cap from the dominant surface tile: sand→sand, snow→snow, mud→jungle grass, ash→ash, stone/marble/granite→stone; infections keep dirt under corrupt/crimson/hallowed grass (how vanilla spreads them). Default dirt/grass.
- **Holes packed, not bridged** — fills unconditionally to `level + Foundation`.

#### Furniture

- **Tombstones** — styles from `ContentSamples.ItemsByType[id].createTile / .placeStyle`, never hardcoded. Placed via `WorldGen.PlaceObject(x, surface-1, …)` which anchors a 2×2 by its lower-left. Clustered on the middle `GraveSpread` of the plot; full-width scattering strands stones down both slopes. `plantable[i]` (set by the profile: `heights[i+1] == heights[i]`) is what guarantees a 2-wide marker has level footing — **this replaced an even-column-snap hack that was a guess**.
- **Angel Statue** (`ItemID.AngelStatue = 52`) — one, on the crown, restricted to the middle third. Placed **before** the graves: it's one object that wants a specific spot, they're many and can go anywhere.
- **Both columns checked** for occupancy before placing anything 2 wide. Checking only the anchor let a tombstone try to grow through the statue's right half; `PlaceObject` refused and the attempt was silently wasted.
- **Fence** is **Wrought Iron**, with 1-in-`IronFenceRarity` panels plain **Iron**. Both resolved via `ContentSamples...createWall` — the wall a fence item places is undocumented. A fence is a **WALL, not a tile.**
  - Built in **runs and gaps**, not a per-tile skip chance: a flat probability gives a nearly solid rail with a few nicks. `MinFenceRun`–`MaxFenceRun` then `MinFenceGap`–`MaxFenceGap`; sections start partway into a run so they don't all begin full-length. ~40% coverage, max run 6.
  - **Span derived from the graves**, not the plot: `left = max(FenceInset, lastStoneMin − FenceMargin)`, `right = min(…, lastStoneMax + 1 + FenceMargin)`. The `+1` is because `lastStoneMax` is an anchor column and markers are 2 wide.
  - **`RemoveFence` must test BOTH wall types.** It originally tested `WallID.WoodenFence` only — left alone after the swap it would have silently left every iron panel standing where a gap was wanted.

#### Current constants
```
PlotWidth 70 · InlandMargin 24 · SearchFraction 0.14f · MinSearchSpan 260 · BeachPull 3
HeadRoom 25 · Foundation 14
MinCrownRise 5 · MaxCrownRise 8 · FlatFraction 0.10f · DomeWeight 0.70f
FootSample 6 · EdgeMargin 4 · BlendTolerance 2 · MaxSkirt 48 · GraveSpread 0.50f
GroundProbe 40 · GroundSolidMin 18 · MinReadableColumns 12 · IslandSkip 20
LivingTreeReach 60 · LivingTreeCap 40000 · LivingWoodWall 244
MinStones 6 · MaxStones 9 · MinStoneSpacing 0 · MaxStoneSpacing 4
FenceInset 2 · FenceMargin 5 · MinFenceSpan 14 · FenceGapChance 12 · FenceMinRun 2
MinFenceRun 2 · MaxFenceRun 6 · MinFenceGap 3 · MaxFenceGap 8 · IronFenceRarity 3
```

**Tuning map:** height → `Min/MaxCrownRise`. Width → `PlotWidth`. Roundness → `DomeWeight` (↑ = domed, ↓ = long even rise). Flat top → `FlatFraction`. Distance from sea → `BeachPull`. Foot blending → `BlendTolerance` / `MaxSkirt`.

#### Tombstone facts (verified in `DK/Terraria.ID/ItemID.cs`)
`Tombstone 321` · `GraveMarker 1173` · `CrossGraveMarker 1174` · `Headstone 1175` · `Gravestone 1176` · `Obelisk 1177`
`AngelStatue 52` · `IronFence 2333` · `WroughtIronFence 4424`

**Side effect:** 5+ tombstones is the **vanilla Graveyard biome trigger** — Ecto Mist, the music, NPC unhappiness nearby.
**Nice detail:** worldgen-placed tombstones get **no `Sign` entry**, so they don't consume the 1000-sign cap.

The **fence layout code (`BuildFence` / `BuildFenceSection` / `RemoveFence`) is largely the user's**; only the material, the run/gap scheme and the span source were changed.

## 4. Errors hit this session and their fixes

| # | Error | Cause | Fix |
|---|---|---|---|
| 1 | `CS1061 'PisPlayer' has no 'BumpScreenShake'` | I read my **workspace** copy, which had drifted from the device | Stage from device before asserting anything exists |
| 2 | `CS0117 'Main' does not contain 'trashItem'` (×3) | It's `Player.trashItem` (`DK/Terraria/Player.cs:1479`), a per-character field | `Main.trashItem` → `Player.trashItem` |
| 3 | `CS1612` on `Main.tile[x,y].HasTile = ...` | `Main.tile[x,y]` returns a `Tile` **by value**; C# forbids assigning to a property of a returned value | Assign through a local `Tile` — it's a handle into shared arrays, so writes still land. **Reads are fine either way**, which is why only assignments were flagged |
| 4 | Graveyard generated nothing | Four causes — see §3.5 | Rewrote `Survey` / profile / palette / `Surface` |
| 5 | Graveyard **still** failed on Large worlds | `Survey` returned false if any 1 of 64 columns couldn't find ground; `Grounded` too strict | Unreadable columns now bridged + scored, not fatal; `GroundSolidMin` 26→18; added `Force()` fallback |
| 6 | Hill was a smooth dome, user wanted stepped terraces | Wrong shape, my choice not theirs | Replaced cosine profile with `FillRun`-based terraces + `plantable[]` mask |
| 7 | "No graveyard on my Large world" | **It generated fine — 1,171 tiles inland.** Score had no distance term | `score -= offset * BeachPull` |
| 8 | Hill came out a **crater** | Claimed measuring from the lowest ground "guarantees clearing the land around it" — only true if rise > slope range. It sat below its neighbours and `HeadRoom` excavated above it | Shape from each column's own ground |
| 9 | Hill came out a **30-tile mountain** | `crownY = highest − rise` is an ELEVATION; `highest` is the tallest point anywhere in the plot, so every column got dragged to it | Additive lift, then trend-line baseline |
| 10 | Hill came out **battlemented** | Additive lift follows the ground contour exactly, inheriting every notch | Arc built on a straight line between the two feet |
| 11 | Ledge at both feet | Baseline reads the ground at the plot edge; ground further out is lower | `BlendSkirt` — step down until within `BlendTolerance` |
| 12 | Hill driven **through a living tree** | Built through the trunk, leaving canopy and roots floating | `FellLivingTrees` flood fill removes the whole tree first |

**Process lesson from #7:** I burned three rounds proposing fixes for a pass that was working. Adding six `Logger.Info` lines answered it in one worldgen run. **Instrument before theorising.**

**Process lesson from #8–#10:** three consecutive shape bugs, each from an unverified claim about geometry stated in a code comment as if proven. #9 was worse: I wrote the exact failure mode down as a caveat in one message and shipped it unchanged in the next. **A simulation of the profile over synthetic terrain catches all three in seconds** — `max step`, `columns below natural ground`, `height above own ground` are the three numbers that matter.

**Recurring lesson:** verify API names and ordering against version-matched source instead of recalling them.

---

## 5. Verified vs Unverified — READ THIS

### ✅ Verified by reading source this session

| Fact | Where |
|---|---|
| `ItemID.ArcSurge = 6173`, `ProjectileID.ArcSurge = 1122` | `DK/Terraria.ID/` |
| Arc Surge item logic | `DK/Terraria/Player.cs:49668` |
| Arc Surge projectile logic | `DK/Terraria/Projectile.cs:71895` |
| `GetSparkleGuitarTarget` (1000×800 rect) | `DK/Terraria/Player.cs:47028` |
| `Player.trashItem` | `DK/Terraria/Player.cs:1479` |
| `Main.mouseItem` | `DK/Terraria/Main.cs:1781` |
| `Player.chest` | `DK/Terraria/Player.cs:2859` |
| Emote/bestiary button coords | `DK/Terraria/Main.cs:43141, 43195` |
| `Main.instance.MouseText(string,int,byte)` is public | `DK/Terraria/Main.cs:20363` |
| `Utils.DrawInvBG(sb, Rectangle, Color)` | used at `Main.cs:20627, 45045` |
| `Main.Map = new WorldMap(maxTilesX, maxTilesY)` | `DK/Terraria/Main.cs:6533` |
| `CalamityUtils.Calamity()` pattern | `CalamityMod/Utilities/GeneralExtensionUtils.cs` |
| `GlobalItem.PrefixChance(Item, int, UnifiedRandom) → bool?` | tModLoader ExampleMod (GitHub) |
| The 6 tombstone `ItemID`s | `DK/Terraria.ID/ItemID.cs` |
| `SoundID.Item93 with { Volume, Pitch, PitchVariance }` compiles | already used in the mod's own `Megashark.cs` |

### ❓ Used but NOT verified for 1.4.4 — check these if the build fails

| API | Where used | Note |
|---|---|---|
| `Player.GetItem(whoAmI, item, GetItemSettings.LootAllSettings)` | `LoreLock.Return()` | 1.4.5 shows a **2-arg** `GetItem(Item, GetItemSettings)`. 1.4.4 arg count unconfirmed. |
| `Player.QuickSpawnClonedItem(source, item, stack)` | `LoreLock.Return()` | tML API, not in `DK` |
| `ItemSlot.Context.ChestItem` | `LoreCodex.DrawSlot` | `ItemSlot.cs` not in `DK` |
| `LegacyGameInterfaceLayer`, `InterfaceScaleType.UI` | `LoreCodex.ModifyInterfaceLayers` | standard tML, unverified |
| `MessageID.SyncChestItem` | `LoreCodex` / `LoreLock` | unverified |
| `ItemIO.Save/Load` + `tag.GetList<TagCompound>` | `LoreLock.SaveData/LoadData` | standard tML, unverified |
| `SceneMetrics.ZoneBeach` formula | discussed for ocean widening | **`SceneMetrics.cs` is not in `DK` at all** |
| `TileID.LivingMahogany` / `LivingMahoganyLeaves` | `GraveyardPass.IsLivingTree` | `TileID.cs` not in `DK`. `LivingWood` IS confirmed (191, used throughout `WorldGen.cs`). If these two fail to compile, delete those two lines — costs nothing outside the jungle |

**If `Return()` fails to compile,** replace it with a plain empty-slot scan over `Player.inventory[0..57]` and a drop at the player's feet as fallback. No risky API needed.

### 🚫 Deliberately avoided
- `Collision.CheckAABBvLineCollision` → hand-wrote `SegmentHitsBox` (slab method)
- `GlobalItem.CanBurnInLava` / `CanResearch` → skipped rather than risk a build error
- `PlayerInput.ScrollWheelDeltaForUI` → used raw `Mouse.GetState()`
- `Main.keyState.PressingShift()` → used raw `IsKeyDown`

---

## 6. Build status

**Unknown.** Two reported errors were fixed (`Main.trashItem`, `CS1612`), but a clean build has **not** been confirmed since. Files not yet proven to compile:

- `Content/PADplayer/PADPlayer.cs`
- `Content/Projectiles/Ranged/SurgeArc.cs`
- `Content/NPCs/PADGlobalNPC.cs`
- `Content/Projectiles/PADGlobalProjectile.cs`
- `Content/Items/PADGlobalItem.cs`
- `Content/Items/LoreItem/LoreLock.cs`
- `Content/UI/LoreCodex.cs`
- `Content/Structures/GraveyardPass.cs` — **this one DOES build and run**, confirmed by `[Graveyard]` log lines and in-game screenshots. The rest are still unproven.

Compiler warnings seen in the log worth clearing:
- `LoreLock.cs(227)` — `Player.QuickSpawnClonedItem` is **obsolete**; use `Player.QuickSpawnItem(IEntitySource, Item, int)`
- `MeleeDecider.cs` — `ReleaseTimer` never assigned, `Meterlogy` never used
- `BladeOfMors.cs(27)` — `ReleaseTimer` never assigned
- Several `PlayerDeathReason.ByCustomReason(string)` obsolete warnings (use the `NetworkText` overload)

**First action in a fresh chat: build, and paste the errors.**

**Session 2 did not build either.** Everything it added is unproven the same way — see
§11.11. It was all syntax-checked (brace/paren balance) and every API name was verified
against version-matched source, but nothing has been through a compiler.

---

## 7. Design decisions discussed but NOT implemented

### 7.1 World size (researched, nothing built)

Numbers worked out from `DK`:

- **Double width AND height = 4× Large**, not 2× (16800×4800 = 80.6M tiles vs 20.16M).
- **`Main.maxChests = 8000` is a `const`** (`Main.cs:1109`). Large already places ~1,500–2,500. 4× the world → 6,000–10,000. `Chest.CreateChest` returns `-1` when full and worldgen just moves on (`WorldGen.cs:52610`) → **silent failure**, structures generated late get no chests.
- **Height is the worse half:** the Underworld is a **fixed ~200 rows** off the bottom (hence `SpawnWOF` refusing above `maxTilesY - 205`), and the cavern layer is already ~97% empty.
- **7 `maxTilesX >= 8400` ladders in `WorldGen.cs` saturate** — e.g. `extraBastStatueCountMax`, dungeon room count. An XL world gets **Large-world density over 4× the area**, silently.
- **Recommendation given: go wide, not tall.** ~12600×2400 gets a vast ocean *and* a surface Kingdom at ~1.5× cost, stays under the chest cap.
- Only live third-party option: **Extra World Sizes** (Huge 10500×3000, 1.4.4). *World Size XL* is abandoned and the author says it's broken.

### 7.2 Ocean widening — `OceanWidthPass` (offered, not written)

All the ocean width numbers are **plain assignable `GenVars` statics**, set together at `WorldGen.cs:11218`:
```
beachBordersWidth = 275 · beachSandRandomCenter = beachBordersWidth + 45
beachSandRandomWidthRange = 20 · oceanWaterStartRandomMin = 220 (Max = Min + 40)
```
Plus five **derived** avoidance vars right below (`evilBiomeBeachAvoidance`, `lakesBeachAvoidance`, `smallHolesBeachAvoidance`, `surfaceCavesBeachAvoidance`, `surfaceCavesBeachAvoidance2`) — re-derive these and corruption/lakes/caves automatically avoid the wider ocean.

**The catch:** `WorldGen.beachDistance = 380` is `public static readonly`, and `NPC.cs` hardcodes ocean spawns against it in **8 places** starting at `NPC.cs:1206`:
```csharp
isBeach = spawnTileY <= Main.worldSurface &&
          (spawnTileX < WorldGen.beachDistance || spawnTileX > Main.maxTilesX - WorldGen.beachDistance);
```
A 700-wide ocean = sharks in the outer 380, dead saltwater beyond. Reflection is unreliable (JIT can inline a readonly static initialised to a literal); **IL editing works** because the compiler emits `ldsfld`. Calamity has an `ILEditing/` folder for the pattern.

Unresolved: `Player.ZoneBeach` comes from `SceneMetrics.ZoneBeach` (a **tile scan**, not the X check), so fishing/music/Fishron may work across a wide ocean — **untested, `SceneMetrics.cs` not available**.

### 7.3 Loi and subworlds

- **Subworld Library** — tML 1.4.3/1.4.4, 169 dependent mods, MP works via a subserver per subworld.
- **Catch:** it **strips Space, both Oceans and the Underworld** from subworlds. The one biome a sky god wants is the one that isn't there.
- **Lore argument raised against it** (from the Lore Bible §IX and §XXIX): Loi *is* the sky, and the flight arc is explicitly about earning permission from *this world's* heavens. Leaving for a different sky undercuts the payoff the flight gating is already building toward.
- **Proposal:** two-stage. The **vessel** fights in the real Space layer of the world; the **true form** in a subworld that has no ground at all (where losing Ocean/Underworld costs nothing).

### 7.4 Claimed-region registry (offered, not built) — **recommended next**

`BloodShrinePass` and `GraveyardPass` are both brute-force searches that **don't know the other exists** and can pick the same shelf today. Proposal: a claim list on `PADStructures`; every pass reserves its rectangle before building and surveys reject overlaps. ~60 lines, makes ten structures as safe as two.

### 7.5 `SurfaceAtlas` (offered, not built)

For big surface structures (the Kingdom of Grace), brute-force sampling collapses. Instead read the landmark vars — **all confirmed present in `WorldGen.cs`**: `GenVars.jungleOriginX`, `snowOriginLeft`, `snowOriginRight`, `dungeonLocation`, `UndergroundDesertLocation`, `leftBeachEnd`, `rightBeachStart` — compute the widest gap, and build there.

Surface budget on Large: ~7,400 after oceans, minus ~2,750 of biomes → **~4,500, but split into 3–4 runs of 1,000–1,500**. Contiguity is the figure that matters, not the total.

---

## 8. Lore

`Terraria_Mod_Lore_Bible.md` sits in the mod root and is **also saved to the Claude Project** as `claude/Terraria_Mod_Lore_Bible.md`, so a fresh chat attached to the "Terraria Mod" project can read it with `project_read`.

Things already implemented that the bible depends on:
- **§XXIX Player Flight** — 3/5 wing time after Empress of Light, capped at 3/5 of Solar Wings; first-kill announcement *"Terrarians are no longer bounded to the ground."* in `(230, 76, 160)`. Lives in `PisPlayer` + `PADWorld` + `PADNPCs`.
- **§XXVII Seal / Wall of Flesh** — the Guide sacrifice cutscene in `Content/NPCs/Legends/GuideSacrifice.cs`.
- **§XVI The Death Daggers** — lore text now wired.

---

## 9. Open TODO

Session 1's list, with session 2's outcomes marked. The live list is §11.11.

1. ~~Build the mod and fix errors~~ — **still open**, and now covers session 2's work too.
2. ~~Decide whether the Codex should count toward lore-item uniqueness~~ — **done**, §11.7.
3. `Item.value = 0` on lore items to close the shop-sell hole. Still open.
4. Redraw `LoreCodexButton.png` (placeholder). Still open.
4b. ~~Swap `QuickSpawnClonedItem` → `QuickSpawnItem`~~ — **still open**; `LoreLock.Return()`
    was not touched in session 2 beyond the uniqueness fix.
5. Build the claimed-region registry before adding structure #4. Still open.
6. `OceanWidthPass` + the `beachDistance` IL edit, if the vast ocean is happening.
7. ~~Update the `pianda-modding` skill~~ — **done**, rewritten and proposed in session 2.
8. Optional: restore vanilla's doll-stack behaviour in `GuideSacrifice`.
9. Optional: gate `GuideSacrifice` on Legends mode.
10. Sweep `NPC.cs` / `Player.cs` for more `maxTilesX` saturation ladders if XL is happening.

## 10. Conventions to keep

- Comments explain **why**, not what — especially non-obvious ordering and netcode reasons.
- Named constants at the top of the class, documented, never magic numbers inline.
- Resolve tile/style from **items** (`ContentSamples`) rather than hardcoding style numbers.
- Prefer hand-written maths over an unverifiable vanilla helper in hot paths.
- Owner-client-only spawning for player-triggered projectiles; let netcode sync outward.
- When vanilla already guards something (e.g. `favorited`), ride on it rather than reimplementing.

---

# SESSION 2

Everything below is session 2. Sections 0–10 are session 1 and have been left as written
except where marked **CORRECTED IN SESSION 2**.

---

## 11.1 Corrections to the setup notes

- **`device_bash` exists, intermittently.** §2 says there is none. During session 2 it
  appeared, vanished, and appeared again as the MCP server reconnected. Do not build a
  workflow on it. The stage → edit → `SendUserFile` → `device_commit_files` loop still
  works whatever happens, so keep using it; check for `device_bash` only as a bonus.
- **`DK` is a full decompile**, not seven files. See §1.
- **The mtime guard trips on Visual Studio re-saves.** It fired twice on
  `Chlorophyte_Rifle.cs` with the byte count unchanged. Re-stage and diff before you
  assume the user edited something; both times the content was identical and committing
  over it was safe. Never force without diffing.

---

## 11.2 The reload sound bug — FIXED

**Symptom:** the reload sound played on a manual reload (the R keybind) and was silent
when a magazine ran dry.

**Cause:** two ways to start a reload, written separately.

- `PisPlayer.TryReload()` called `held.PIS().StartReload()` and then played
  `SoundID.Item149` **at the call site**.
- `PSItem.Shoot()`, on an empty magazine, did `ReloadTimer = ReloadTime;` **directly** —
  never going near `StartReload`, and so never near the sound.

**Fix:** one private `BeginReload(Player)` inside `PSItem` that both paths now call. The
sound lives inside it. `StartReload` takes a `Player` now (pass null for silence) — that is
the only signature change, and `PisPlayer.TryReload` no longer plays anything itself.

**The general lesson, which recurs below:** when two call sites do "the same thing", the
thing needs to be one function. Anything added to one of them later is a coin flip.

---

## 11.3 Chlorophyte Rifle — NEW

`Content/Items/Ranged/Chlorophyte_Rifle.cs` (the user wrote the initial file; session 2
built the behaviour), `Content/Items/Ranged/Chlorophyte_Rifle.png` (redrawn, §11.8).

### What it does

- **Ordinary shot:** one vanilla `ProjectileID.BulletHighVelocity` at `1.8×` the burst's
  launch speed. Carries the streak damage bonus.
- **Burst:** landing 5 consecutive hits arms the gun; the next pull fires **10 vanilla
  `ProjectileID.ChlorophyteBullet`** scattered randomly inside **±7°** with speeds rolled
  `0.75×`–`1.25×`. Fires at base damage — no streak bonus.
- **On any hit:** a spray of chlorophyte leaves that purifies the ground it drifts across.
- Magazine 3, reload 60, damage 70, `useAmmo = AmmoID.Bullet`, ammo-agnostic (any bullet
  is accepted, every round comes out chlorophyte).

### It fires no custom projectiles

Both round types are vanilla. There is no `ModProjectile` to hang behaviour on, so
`PADGlobalProjectile` stamps `ChloRifle` in **`OnSpawn`** by reading the spawn source:

```csharp
if (source is EntitySource_ItemUse use && use.Item?.ModItem is Chlorophyte_Rifle)
    ChloRifle = true;
```

`EntitySource_ItemUse_WithAmmo` derives from `EntitySource_ItemUse`, so one test catches
both the primary and the burst. Passing `source` through to the burst's `NewProjectile`
calls is what makes the burst rounds spill too — drop that and they are inert.

The flag is local and never synced, which is fine **because everything it drives runs
owner-side**. If that ever stops being true, this needs netcode.

### Shoot returns false, always

`Shoot` spawns every round by hand. The two paths need different damage and `Shoot`
receives `damage` by copy, so it cannot change what the caller fires. Handing the caller
nothing is what allows the bonus to land on the single shot and deliberately not on the
burst.

### The combo lives on PisPlayer

`ChloCombo`, `ChloComboTimer`, `ChloArmed`, and `ChloComboRequired = 5`,
`ChloComboDecay = 180`, `ChloDamagePerStack = 0.08f`, `ChloMaxStacks = 5`.

**On PisPlayer and not PADPlayer** because `PADPlayer` is wiped by `ResetEffects` every
tick and a combo that survives less than a frame is not a combo. Same rule as the camera
and the shake.

Three rules, all of which exist because of a bug they would otherwise cause:

| Rule | Why |
|---|---|
| `firstHit` captured before `chloConnected` is set | A round that pierces two enemies is **one shot**. Without it, one bullet through a crowd arms the burst by itself. |
| Only `BulletHighVelocity` advances the combo | The burst throws ten rounds at once; letting them count means every burst instantly re-arms the next and the gun never leaves burst mode. |
| Only `BulletHighVelocity` *breaks* the combo | Burst rounds mostly miss and expire a second later, by which time a fresh streak may have started. Letting those expiries break it would kill every streak begun after a burst. Chlorophyte rounds are neutral in both directions. |

Breaks on: a miss, 3 seconds without a hit, weapon swap, death. **Not** on reload — with a
3-round magazine and a 5-hit requirement, a reload-reset makes the burst unreachable by
arithmetic. That was going to be a rule and had to be dropped.

---

## 11.4 `ChlorophyteSpill` — NEW, and a model that took three rewrites

`Content/Particles/ChlorophyteSpill.cs` + `.png`. Also contains `ChlorophyteBloom`, a
`ModSystem` (precedent: `SurgeArc.cs` holds two classes).

### The leaf model

Two lives, handed over on a timer from tick 45 to 85:

- **Before:** weightless. Drag on both axes, no gravity, no sway. Coasts out on the throw.
- **After:** gravity `0.14`, terminal fall drops `12` → `1.15`, and it sways, rocks against
  the sway, and turns edge-on and back.

The edge-on turn is `drawScale.X *= |cos(swayPhase)|*0.75 + 0.25` — squeezing the width on
the same phase that drives the sway. Two lines, and it is the difference between a leaf
tumbling and a sprite sliding sideways. Never reaches zero; a leaf perfectly edge-on for
one frame reads as a blink.

Every leaf gets its own sway phase, rate and strength. Shared values make forty leaves
move as one sheet of cloth — the single most important detail in the effect.

### Three bugs the simulation caught before they shipped

Written up because all three were invisible in code review and obvious in ten lines of
Python:

1. **Flutter deadlocked.** Driven off speed: flutter *lowers* the terminal fall, but the
   un-lowered fall (4.2) sat above the flutter threshold (3.2). A leaf at rest still
   measured "fast", never crossed over, fell like a stone forever. Sim showed `flutter`
   at 0.00 at every sample past tick 45. **Now driven by age** — no feedback path.
2. **The sway fed the same loop.** A `0.085` push against `0.977` drag settles at
   `3.61 px/tick`, on its own above the threshold. **Now a bounded position offset**, not
   an acceleration, so it cannot accumulate or feed back.
3. **Leaves sailed.** A 7.5px throw under the old drag drifted **20 tiles** sideways.
   Drag is `0.952` now.

A fourth was caught the same way: cutting gravity while fluttering left a leaf thrown
upward coasting nine tiles into the air for a second and a half. The terminal clamp alone
does the slowing now.

### The compounding fade — a live bug elsewhere in the mod

The particle was invisible in game. Cause:

```csharp
Color = Color * (1f - t * t);   // WRONG — re-fades an already faded colour
```

This compounds. Over a 60-tick life it falls below **5% brightness by tick 31** and reaches
zero before the particle is half done. It should be 75% at tick 30. Fixed by storing
`initialColor` and deriving from it each tick.

**`Content/Particles/AshDissolve.cs` line 75 has the identical pattern and was NOT fixed.**
The ash dissolve is very likely fading at roughly twice its intended rate. Left alone
because nobody asked; worth a look.

### Purification is the trail, not a disc

There is no converted square. Each leaf converts the tile it enters, gated on actually
crossing a tile boundary — a leaf drifting 2px/frame would otherwise re-convert the same
tile twenty times. Capped at 16 tiles per leaf, 20 purifying leaves per burst. Only the
firing client's leaves write tiles; every client draws its own spray.

`ChlorophyteBloom` collects every conversion in a tick into one rectangle and sends a
single `SendTileSquare` at the end of it. A packet per tile would bury a connection.

### `SpawnBurst` normalises its direction

```csharp
SpawnBurst(Vector2 position, Vector2 direction, float reachTiles, bool purify, float speedScale = 1f)
```

**Multiplying the `direction` argument does nothing** — it is normalised on the way in, so
only the angle survives. The user tried `projectile.velocity * 1.5f` and it was a no-op.
`speedScale` is the real knob; `ThrowStrength` at the top of the file is the base.

---

## 11.5 `SlashStreak` — NEW, and the mod's first additive particle

`Content/Particles/SlashStreak.cs` + `.png`. Replaces the King Slime death slash, which
was three scaled `MagicPixel` bars drawn in `LegendsSlimes.PostDraw`.

**Why the old one looked pixelated:** a 1×1 pixel scaled to 26px wide is a hard-edged
rectangle. No colour makes a hard-edged rectangle look like light.

**What Calamity actually does**, from `Particles/ThinEndedLine.png`, `BloomLine.png`,
`FadeStreak.png`, `BloomLineVFX.cs`, `LineVFX.cs`, `SlashThrough.cs`:

1. **A soft white mask, not a pixel.** Their line textures are white with a smooth falloff
   across the width and a taper along the length — 114 to 224 distinct alpha levels.
   `SlashStreak.png` is our own: 48×256, gaussian across, sin-tapered along, 256 levels.
   Height stretches to the line's length and width to the thickness, independently, so one
   mask serves both the needle and the halo.
2. **`UseAdditiveBlend`.** Every one of their line effects sets it. Layers *sum* toward
   white instead of covering each other, and the glow spills onto the surroundings.
3. **It is a particle, not a `PostDraw`** — as their `SlashThrough` is. That gets the
   additive draw for free and puts it at the dust layer without `LegendsSlimes` having to
   close and reopen the sprite batch mid-frame to change blend state. It follows the NPC
   while alive, as theirs does with `StuckTo`.

The animation (head in 12%, tail from 16%, fade over the last quarter, 30 ticks) is
unchanged from session 1 — that part was fine. One addition: a brightness spike as the
head lands, so impact reads as a flash.

`PostDraw`, `DrawSlash` and `Bar` are gone from `LegendsSlimes.cs` (68 lines). `SlashShow`
and `SlashWidth` stay; the other six slash constants moved into the particle.

**This is the first thing in the mod to use the additive bucket** `PADParticleHandler`
already had. If it looks too hot, the three strengths at the bottom of `CustomDraw`
(`0.55`, `0.85`, `1.0`) are the dials.

---

## 11.6 `DentedMegashark` — NEW

`Content/Items/Ranged/DentedMegashark.cs`. A deliberately worse Megashark.

Vanilla baseline from `DK/Terraria/Item.cs` `case 533`: damage 25, use 7, shootSpeed 10,
knockback 1, rare 5, 35 gold. Measured against **this mod's** Megashark (`PSItem` sets use
6, magazine 98 / reload 160), not vanilla's.

| | Megashark (this mod) | Dented |
|---|---|---|
| Damage | 25 | 19 |
| Use time | 6 | 9 |
| shootSpeed | 10 | 8 |
| Knockback | 1 | 0.6 |
| Magazine | 98 / 160 | 60 / 220 |
| Spread | none | ±6° |
| Jam | none | 8% per round, 22 ticks |

**The jam rolls in `Shoot`, after a round fires** — not in `CanUseItem`, which is polled
more than once per shot and would drift the probability. A jam is the *next* round failing
to chamber, which is what a jam is.

**`GetAlpha`, not `Item.color`.** Verified in `DK/Terraria.DataStructures/PlayerDrawLayers.cs`
(lines 3251, 3344, 3358, 3376, 3435, 3451): the held-item draw path calls
`heldItem.GetAlpha(...)` and never reads `Item.color`. Tinting via `Item.color` gives a
rusty gun in the pack that turns showroom-fresh the moment it is held.

Borrows `Terraria/Images/Item_533` for now. A dented sprite is a `sprite-helper` job.

**Not wired to the boss.** `Megashark.cs:685` still drops a pristine `ItemID.Megashark`,
which sits oddly against Lore Bible §XXII (Megashark is a boss, not a craftable weapon) and
against `PADRecipes` deleting its recipe. Swapping the drop is one line.

---

## 11.7 Lore item uniqueness — FIXED

Closes session 1's known gap and TODO #2.

**Symptom:** the same lore item could sit in the inventory *and* on the Codex shelf.

**Cause:** `LoreLock.PurgeDuplicates()` walked `Player.inventory` only, and
`LoreCodex.Click`'s `Holds()` walked the codex list only. Neither ever saw the other, so
shelf-plus-pack passed both tests.

**Fix, in `LoreLock.cs`:**

- New public `CodexHolds(int type)`.
- `PurgeDuplicates` runs a codex pass **first**: any loose lore item whose type is already
  shelved is destroyed with the existing "There can only be one." line. The shelved copy is
  the survivor — shelving is deliberate, a loose copy appearing after is the newcomer.
- `LoadData` dedupes the codex itself, so the fix repairs characters saved before it.

Withdrawing is unaffected: taking an item off the shelf removes it from the codex in the
same click, so there is never a tick where both hold it. `RescueTrash` and
`RescueContainer` run before the purge, so a rescue into a conflicting pack settles the
same tick.

`LoreCodex.Click` still has its own private `Holds` duplicating `CodexHolds`. Six lines,
correct, not collapsed.

---

## 11.8 Sprite work, and the new `sprite-helper` skill

### The Calamity house style, measured

From 18 rogue daggers spanning pre-boss to endgame:

1. **Alpha is binary** — 0 or 255, zero semi-transparent pixels in all 18. (Particle
   *masks* are the exception; those are soft on purpose.)
2. **No black outline.** Only **17–48%** of a silhouette's perimeter is darker than the
   pixel behind it. The rim goes on the shadow side only; the lit edge runs from its
   brightest pixel straight to transparency. 5–9 distinct edge colours per sprite, not one
   shared black. **Dark guns are the exception** — `PestilentDefiler` has a 100% flat
   near-black keyline, and it works because its body is nearly as dark.
3. **Orientations cluster hard.** Daggers +41–48° or ±90°. **Guns −4° to +8°**, near
   horizontal — a third class the dagger rule does not cover.
4. **Palettes:** daggers 11–17 colours; guns 19–25.
5. **Shadows go cool** — every ramp's dark end lands in desaturated purple-navy whatever
   the material's hue. Highlights push to cream. Magic items spend a pure `#FFFFFF`.
6. **The highlight is a band along the length**, not a gradient across it — 11 of 13
   elongated daggers peak on the spine, and neither of the other two peaks on an outer
   edge. An early claim of "light from above" was wrong and was corrected by measurement.
7. **Even boxes, loose packing.** Diagonals fill only 26–40% of their box.

### Chlorophyte_Rifle.png

The user's rough measured **100% uniform on a 2×2 grid** — a 49×18 drawing saved at 98×36,
so every apparent pixel was a 2×2 block. Recolouring it could never be pixel-perfect; it
was redrawn at native 98×36 (now 69% uniform). Mahogany stock with grain, dark blue-violet
gunmetal, faceted chlorophyte, brass muzzle band, near-black keyline. The rough is kept as
`Chlorophyte_Rifle_rough.png`.

**Test for this whenever a sprite looks chunky**: check whether every 2×2 (or 3×3) block is
uniform. It is the difference between a bad drawing and a good drawing at the wrong size.

### Particle masks are white

`BloodDroplet.png` is a 10×10 **white** mask with soft alpha; all the colour comes from
`Color`. That is why it tints to anything and why it does not read as a recoloured sprite.
`SlashStreak.png` follows it. The binary-alpha rule does **not** apply to masks.

### The skill

`sprite-helper` was created and proposed this session. It carries the rules above, the
stage/commit loop, the pass order (de-alias → silhouette → palette → shading → rim → box),
and a calibrated `spritecheck.py` that all 18 reference daggers pass clean.

**Its axis and spine checks are dagger-derived and flag guns as false positives.** Two of
its checks were wrong on first write — a vertical light test a diagonal blade defeats, and
an axis test applied to a round flail — and only surfaced by running them against the
reference set. It needs a gun profile.

---

## 11.9 Verified this session

| Fact | Where |
|---|---|
| Megashark `case 533`: damage 25, use 7, speed 10, kb 1, rare 5 | `DK/Terraria/Item.cs:7722` |
| `ProjectileID.ChlorophyteBullet = 207` | `DK/Terraria.ID/ProjectileID.cs:866` |
| `ProjectileID.BulletHighVelocity = 242` | `DK/Terraria.ID/ProjectileID.cs:936` |
| `BiomeConversionID.Purity = 0` | `DK/Terraria.ID/BiomeConversionID.cs` |
| `WorldGen.Convert(i, j, type, size, bool, bool)` in 1.4.5 | `DK/Terraria/WorldGen.cs:55550` |
| `WorldGen.InWorld(int, int, int fluff = 0)` | `DK/Terraria/WorldGen.cs:9642` |
| `NextVector2Circular`, `NextVector2CircularEdge`, `NextVector2Unit` | `DK/Terraria/Utils.cs:1296-1308` |
| `RotatedBy(this Vector2, double, Vector2 = default)` | `DK/Terraria/Utils.cs:1752` |
| `CombatText.NewText(Rectangle, Color, string, bool, bool)` | `DK/Terraria/CombatText.cs:59` |
| Held items draw through `heldItem.GetAlpha(...)`, never `Item.color` | `DK/Terraria.DataStructures/PlayerDrawLayers.cs:3251+` |
| `NextFloat(min, max)` is a **tModLoader** addition, not in vanilla `Utils` | absent from `DK/Terraria/Utils.cs`; already used in this mod's `AshDissolve.cs:47` |
| Calamity line VFX are white masks + `UseAdditiveBlend` | `CalamityMod/Particles/{BloomLineVFX,LineVFX,SlashThrough}.cs` |

### Two traps worth their own entry

**`WorldGen.Convert(i, j, type, size)` does not mean by "size" what it looks like.** Its
inner loop is gated on a hardcoded

```csharp
Math.Abs(k - i) + Math.Abs(l - j) < 6
```

so it can never reach past about five tiles however large a `size` is passed. Asking for
ten blocks silently gives five. **Call it with `size: 0`** — that collapses the loop to the
single centre tile — and drive the shape yourself.

**`BiomeConversionID` does not exist in 1.4.4.** It arrived in 1.4.5. The numbering matches
across both, so write the literal `0` for Purity. `WorldGen.Convert` also gained its
`tiles`/`walls` parameters in 1.4.5; the 4-argument call compiles against both.

---

## 11.10 Process notes

- **Simulate before shipping motion.** Every bug in §11.4 was found in ten lines of Python
  and none would have survived to a build. Model the update loop, print the trajectory.
- **Calibrate a check against known-good data before trusting it.** `spritecheck.py` failed
  8 of 18 reference sprites on first write. The check was wrong, not the sprites — and the
  fix corrected a claim already made to the user.
- **"The same thing done twice" is where bugs live.** The reload sound, the lore
  uniqueness, and the two combo sites were all one behaviour written in two places.
- **Re-stage before editing, every time.** The user edits between turns, often mid-task.
  Two commits were rejected by the mtime guard in this session alone.
- **Two instructions were misread and had to be reversed**: "make the particles go way
  back" meant *far*, not *backwards*; and the user's own `* 1.5f` on a normalised vector
  was a no-op nobody had noticed. When a tweak has a plausible reading that changes
  direction rather than magnitude, ask.

---

## 11.11 Open TODO after session 2

**Blocking:**

1. **BUILD.** Nothing from either session is confirmed compiling. New/changed this session:
   `Chlorophyte_Rifle.cs`, `PADGlobalProjectile.cs`, `ChlorophyteSpill.cs` + `.png`,
   `SlashStreak.cs` + `.png`, `DentedMegashark.cs`, `PSItem.cs`, `PisPlayer.cs`,
   `LoreLock.cs`, `LegendsSlimes.cs`, the `.hjson`.
2. **Delete `Content/Projectiles/Ranged/ChlorophyteBullet.cs` and `.png`.** A custom
   projectile built and then vetoed in favour of the vanilla one. It compiles as a harmless
   unused projectile, but its class name collides conceptually with
   `ProjectileID.ChlorophyteBullet` — a file holding both is a trap. The session had no
   delete tool; this needs doing by hand.

**Follow-ups from this session:**

3. `AshDissolve.cs:75` has the compounding-fade bug (§11.4). Probably fading at twice its
   intended rate.
4. Give `spritecheck.py` a gun profile — horizontal axis, full keyline allowed, wider
   palette (§11.8).
5. Decide whether the Megashark boss should drop `DentedMegashark` instead of the pristine
   one (§11.6).
6. `PSItem.MuzzleOverrides` is keyed by `ItemID`, so `DentedMegashark` gets the default
   muzzle position and its flash sits short of the barrel. Cosmetic.
7. A real dented sprite for `DentedMegashark`, instead of the tinted vanilla one.
8. Collapse `LoreCodex.Click`'s private `Holds` into `LoreLock.CodexHolds` (§11.7).
9. `ChlorophyteSpill` leaves drift through terrain — `PADParticle` has no tile collision —
   so purification continues downward through ground. Bounded by the 16-tile cap, but a
   collision check in `Update` would stop it at the surface if that reads better.

**Still open from session 1:** items 3, 4, 4b, 5, 6, 8, 9, 10 in §9.

---

# SESSION 3

> Covers the Wasted Mile biome, the Abomanation boss, the player ragdoll, the mod-wide
> CC-bypass tell, and — most usefully for whoever reads this next — **a working local
> compiler**, §12.9. Read that section before writing a line of code.
>
> Where session 3 disagrees with 1 or 2, session 3 wins.
>
> **Start here:** §12.9 (compile before shipping), §12.10 (verified API facts, including
> three that cost a session each), §12.12 (live TODO).

---

## 12.1 The Wasted Mile — biome, shader, worldgen

A surface region on the side of the world away from the ocean, inside `Layout.Mile`. Ash,
trenches, cold grey, nothing alive.

| File | What it does |
|---|---|
| `Content/Biomes/WastedMileBiome.cs` | `ModBiome`. `Covers(tileX, tileY)` is the public test: inside `Layout.Mile` **and** `tileY <= Main.worldSurface`. `Priority => BiomeHigh`, `Music => 0` (silence), `SpecialVisuals` drives the filter. |
| `Content/Biomes/WastedMileSurfaceBackground.cs` | `ModSurfaceBackgroundStyle`. Far/middle/close from `Content/Biomes/Backgrounds/WastedMile{Far,Middle,Close}.png` (1024×500 / 500 / 400). `CloseLift = 90f`, applied as `b -= CloseLift`. |
| `Content/Biomes/WastedMileCritters.cs` | `EditSpawnPool` strips every critter when `WastedMileBiome.Covers(SpawnTileX, SpawnTileY)`. Nothing small spawns on the Mile. |
| `Effects/WastedMile.fx` | Cold grade: Drain 0.70, Shadow/Light, Grade 0.5, Flatten 0.88, Ceiling 0.92, Vignette 0.6. Pass `MilePass`. **No grain** — see below. Needs EasyXnb compilation. |
| `PisAndDag.cs` → `LoadWastedMileFilter()` | Registers `"PisAndDag:WastedMile"`, `UseIntensity(0.85f)`. |

**Two visual bugs worth remembering.** The "rectangle" in the far background was a haze
gradient band baked into the PNG, not a rendering fault. The "static TV overlay" was a
grain term in the shader. Both removed. The brief was *eerie, cold, lonely* — not noisy.

**`WastedMileFillPass`** in `Content/Structures/PADWorldLayout.cs`: after every carve and
plant pass, before "Final Cleanup". Fills every non-solid tile (pots, vines, chests, water,
air) from the ash surface down to `Depth = 200`, `EdgeWobble = 60`. Chests are unhooked via
`Chest.FindChest` / `DestroyChestDirect` before their tiles go.

> **`AshTop` must scan from `y = 10`, not from near `Main.worldSurface`.** On a Grand world
> the Mile's ridge sits more than 120 tiles above `worldSurface`, so a scan window starting
> at the surface finds no ash and the pass silently skips every column. It logs
> `Wasted Mile fill: {columns} of … columns had ash at the surface, {filled} tiles packed` —
> if columns is 0, this is why.

---

## 12.2 The Abomanation — files and shape

A fifteen-segment worm boss. Summoned on the Mile by releasing critters, not by an item.

| File | Contents |
|---|---|
| `Content/NPCs/Abomanation_Small/Abomanation.cs` | Constants, `AbomanationArms` (arm sprite placement), `AbomanationChain` (linking, following, the whip swing, contact, the throw). 70 KB — the biggest file in the mod. |
| `AbomanationHead.cs` | The AI. Phases, arrival, the whip, second wind, `PreKill` loot relocation. 52 KB. |
| `AbomanationBody.cs` | One class for all five body sprites; the sprite is picked from `ai[3]` at draw time, so five textures cost one NPC type. |
| `AbomanationTail.cs` | The tail. |
| `AbomanationSummon.cs` | The critter-release counter. |
| `AbomanationHints.cs` | Clothier dialogue + the critter tooltip. |
| `AbomanationCommand.cs` | `/abomanation` debug spawn. |
| `BossLogSupport.cs` (mod root) | Boss Checklist entry with a custom portrait. |

**Chain links live in `ai[]`, not in fields**, because `ai[]` rides the NPC sync packet:
`ai[0]` = sprite mirrored, `ai[1]` = index ahead + 1, `ai[2]` = head index + 1, `ai[3]` =
place in chain. `localAI[0..2]` hold the whip's per-link angle, spin and serial — local
only, never synced, which is fine because the swing is deterministic from synced state.

**Phases** (`phase` field, synced): −1 arrive, 0 chase, 1 dive, 2 rush, 3 fall, 4 whip
run-in, 5 loop, 6 crack.

**Second wind** at `RagePoint = 0.4` of max life: speed ×1.2, whip every rush with a 4 s gap
instead of 1-in-3 with a 20 s gap, one low roar.

**Segments carry `NPC.PAD().OwnIntro = true`.** Without it `BossArrivalWait` holds every
boss-flagged segment still for the universal arrival and the head swims off alone, leaving
a headless body on the ground. This cost a debugging session.

**Loot drops at the nearest surviving segment**, not at the head, because the head is
usually underground when it dies (`PreKill` moves `NPC.position`).

---

## 12.3 The whip — the one move worth reading about

> The head runs the player down, stops short, turns once in a tight loop so the body coils
> behind it, then drives straight out of the coil. The body follows with the turn still in
> it and the tail leaves the animal in one wide arc.

**Nothing in it is scripted.** During the crack each link is a damped spring in *angle*
toward the link ahead (`AbomanationChain.Swing`): stiffer toward the tail, turn rate capped.
The coil, the wave down the body, the arc and the recoil all fall out of the head's two
moves plus that one rule.

Measured in `/home/claude/work/whip/pend2.py` (rebuild it if needed; it is fifty lines):

| | |
|---|---|
| loop | 70 ticks |
| tail peak | **60 px/t** at tick 24 of the run — where `Item153` plays |
| tail over 25 px/t | 46 ticks |
| reach | the whole body, 42 tiles, straight out at full arc |
| settled | 93 ticks; the head resumes chasing at 50 while the body finishes |

Key constants: `WhipLoopSpeed 9`, `WhipLoopRadius 100`, `WhipRunSpeed 10`,
`WhipSpringHead 0.05` → `WhipSpringTail 0.38`, `WhipDamping 0.90` falling to `0.60` from
tick 25, `WhipMaxTurn 0.16` rad/t.

**The loop radius is deliberately roomy.** A tight loop leaves the body straight with a kink
at the neck instead of a coil you can see, because the follow chain cuts corners.

**Three failed designs, recorded so nobody rebuilds them:** a rigid rod about a fixed
segment (reads as a bar); the same rod with a scripted wave (a wave through a bar); and a
Verlet rope with a length constraint — the right *shape*, and a tail at **280 px/t**
whatever the head did, because an inextensible chain puts everything into its tip. Slowing
the head did not slow the tip. The angular chain keeps the shape and caps the tip.

**Aiming is which way the loop turns.** The tail comes off the coil on the side the loop did
*not* turn toward. Player left of its line → it circles right → the lash takes the left.
Dead ahead (within 24 px of the line) it circles so the lash comes over the top rather than
under the ground.

**Hits are swept**, not point tests: `Collision.CheckAABBvLineCollision` along the line the
segment moved this tick, 52 px wide. At 60 px/t a point test misses a standing player about
half the time. Damage ×1.25 at the neck rising to ×2 at the tail.

---

## 12.4 The throw, and `RagdollPlayer`

`Content/PADplayer/RagdollPlayer.cs`. A lash hit writes straight onto `Player.velocity`, so
Cobalt and Ankh Shield do nothing — it is not knockback, it is being hit by something heavy
moving fast.

Direction is the **unit tangent of the segment's own path this tick**, magnitude its speed
along that path × `WhipToss 0.34` × `WhipTossBoost 4`, floor 8 px/t, no ceiling, no lift.

Then the body tumbles: no control, no items, wings and boots inert. Spin if thrown upward
past 8 px/t, roll otherwise. Bounces: floor 0.45, ceiling 0.45, wall 0.5, sideways ×0.75 per
floor bounce, re-spun from the new arc. Under `RestSpeed 2.5` it lands and lies 90 ticks,
then a smoothstep stand-up.

**Three bugs, all non-obvious, all fixed — do not reintroduce them:**

1. **The throw must be applied in `ModPlayer.PostHurt`.** Vanilla `Player.Hurt` writes its
   own knockback onto velocity at IL `0x05c2`, *after* `OnHitPlayer` and *before* `PostHurt`
   at `0x096c`. A throw written in `OnHitPlayer` is silently replaced by a three-pixel shove.
   The hit stores a pending throw; `PostHurt` (and `PostUpdate`, for throws outside a hurt)
   applies it.
2. **The lash needs its own immunity slot.** A brush from the head or the coil leaves 40
   ticks of general immunity, and the crack lands inside that window — so
   `Update_NPCCollision` skipped the hit before any hook ran, and the player never flew.
   `CanHitPlayer` sets `cooldownSlot = ImmunityCooldownID.DD2OgreKnockback` while lashing.
   *This looked exactly like "wings and rocket boots are immune to the ragdoll". It was not
   flight. It was i-frames.*
3. **Contact is read as speed lost, not speed at zero.** Above 16 px/t `Player.DryCollision`
   moves the body in 16 px steps and returns the sum of the steps it completed, so a body
   that met the floor on step three of five still comes out with two steps of fall on it. A
   `velocity.Y == 0` test never sees it. Compare `before` (captured in `PreUpdateMovement`)
   against the post-move velocity with a 0.05 slack.

Also: `WhipTossLift` is 0, and the floor clamp must be skipped when it is 0 — `Y > -0 →
Y = 0` was zeroing every downward throw. And vanilla clamps falling to `maxFallSpeed` every
tick, which turned a downward throw into a drift; `PostUpdateRunSpeeds` raises it while
airborne.

---

## 12.5 `CCBypass` — the mod-wide tell for CC-immune attacks

`Content/Combat/CCBypass.cs`. **A rule, not a feature:** anything in the mod whose hit goes
through crowd-control immunity announces itself, always the same way, so the player learns
one signal.

- **The colour** is `CCBypass.Warning` = `(255, 222, 66)`. Nothing else in the mod is that
  yellow.
- **The flag.** An NPC or projectile sets `PAD().CCBypass = true` every tick it is in the
  state. The globals clear it before each AI, so the owner never clears it.
- **The outline.** Eight copies of the entity's own sprite in the warning colour with
  `A = 0`, 3 px out in a ring, drawn *before* the sprite so only the ring shows. Same method
  as the Greatbow's charge glow. Handled automatically by `PADGlobalNPC.PreDraw` /
  `PADGlobalProjectile.PreDraw`.
- **`ICCBypassOutline`** for an entity that draws something other than its registered
  texture — the body segments implement it because five sprites share one NPC type.
- **The star and the note.** On the tick the flag comes on, unless `PAD().CCQuiet` is set.
  `CCBypassStar` is a `PADParticle`: a soft white-on-transparent four-point star
  (`Content/Combat/CCBypassStar.png`, 34 px, premultiplied, Calamity `Sparkle2` style),
  drawn additively in the yellow at scale 0.85, riding the entity's centre, blinking 3 ticks
  on / 3 off for 30 ticks. `Content/Sounds/CCBypassWarn.ogg` plays once, one instance.
- **`CCQuiet`** is for parts of a many-part thing. The fifteen segments are quiet; the head
  calls `CCBypass.Warn(NPC)` once, as it stops to loop — half a second of coil before
  anything can throw.

**The outline is one silhouette round the whole animal, not a ring per segment.** Vanilla
draws NPCs from slot 199 downward, so `AbomanationChain.DrawsFirst` finds the highest slot
that is on screen (mirroring vanilla's 800 px cull) and that part draws
`AbomanationChain.Silhouette` for every part, before any sprite. Body and tail both draw
through one `DrawWhole(spriteBatch, screenPos, colour)` so the outline traces exactly what
is drawn, arms included.

**New attacks that ignore knockback, shields or i-frames set the flag or they do not ship.**

---

## 12.6 Summon, hints, boss checklist

**Summoning.** Release a bunny, squirrel or red squirrel on the Mile. It is taken; the tenth
brings the boss. The count is `PADWorld.MileOfferings` — **world-saved, not per-player**, so
two players can feed it between them.

> A released critter carries **`EntitySource_Parent`** (via `NPC.ReleaseNPC` →
> `GetSource_ReleaseEntity`), with `Entity` = the player. It is *not* `EntitySource_ItemUse`.
> Testing for `ItemUse` first is why nothing was consumed; then testing for both gives
> CS8510, because `EntitySource_ItemUse : EntitySource_Parent`. One `is EntitySource_Parent`
> test is correct and sufficient.

**Clothier hint**, 1-in-3 of his lines until `DownedAbomanation`, key
`Mods.PisAndDag.Hints.ClothierMile`. Elden Ring Patches tone. Critter tooltip
`Hints.Offering`: *these may satiate one and many*.

**Boss Checklist portrait.** `customPortrait` is an `Action<SpriteBatch, Rectangle, Color>`.
The portrait was built by collapsing the doubled sprites 2×2 back to native, arranging the
chain on a 303° ellipse, then exporting at 2× nearest: 334×370, premultiplied. Head grid
offset is `x = 1` — without it the head sits visibly off the coil.

---

## 12.7 Textures — the rule that keeps biting

> **tModLoader never premultiplies.** `ImageIO.ToRaw` and `RawImgReader.FromStream` copy
> RGBA straight through, and the game blends premultiplied. Every PNG the mod ships must be
> premultiplied by hand, or it renders as white blobs wherever alpha is partial.

Related: PIL's `ImageDraw.ellipse` **overwrites** alpha rather than accumulating it, so
overlapping soft blobs come out flat. Accumulate alpha in numpy, then premultiply, then
write.

---

## 12.8 Lore written this session

`Terraria_Mod_Lore_Bible_ver_1.1.md` §XXI:

- **The Abomanation** — a subject of the Artificers' programme, left where the programme
  ended. Boss checklist image embedded (relative path, so it only resolves for someone who
  has the mod folder).
- **How it looks** and **How it fights** — [LOCKED] read of the sprite and the fight.
- **The second hand** — *"A second hand appears in the margins of the record, and it is not
  recording results."* One line. Personal cruelty running alongside the clinical register,
  nothing named.
- **The two of them** — the small one from the villages (farmers, the ones nobody would ask
  after); the other from knights, men with reputations, soldiers off both siege lines, and
  goblins at the end. **[LOCKED]** the second is post-Moon Lord.

`PAD_NPC_Plan.md` §2.6 (the whip, measured) and §2.7 (the CC-bypass rule).

**Voice reminders:** no em dashes in in-game text. The `pad-lore-voice` skill holds the
rules; §XXVII is the method — nothing about the Abomanation is explained anywhere in the
world, and nothing should be. Watch for the LLM tic of *"statement, then flat negation"*
(*The programme ended. It did not.*); the user will catch it and it reads as AI.

---

## 12.9 **A local compiler — set this up first, every session**

Two compile errors reached the user before this was built. It takes about five calls and it
ends the problem.

```bash
# 1. The SDK. dot.net and NuGet are blocked by the egress proxy; Ubuntu's apt is not.
apt-get update -qq && apt-get install -y --no-install-recommends dotnet-sdk-8.0

# 2. Stage the reference assemblies from the user's Steam install (connected folder):
#    Libraries/FNA/1.0.0/FNA.dll
#    Libraries/ReLogic/1.0.0/ReLogic.dll
#    Libraries/TerrariaHooks/0.0.0.0/TerrariaHooks.dll
#    Libraries/monomod.utils/25.0.10/lib/net8.0/MonoMod.Utils.dll
#    Libraries/mono.cecil/0.11.6/lib/netstandard2.0/Mono.Cecil.dll
#    Libraries/log4net/2.0.8.0/log4net.dll
#    Libraries/newtonsoft.json/13.0.3/lib/net6.0/Newtonsoft.Json.dll
#    Libraries/microsoft.codeanalysis.common/4.12.0/lib/net8.0/Microsoft.CodeAnalysis.dll
#    plus tModLoader.dll from the install root

# 3. Stage all ~118 .cs files from the mod (50 per call), overlay your edits, then:
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll)
REFDIR=$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
# -r: every dll in $REFDIR plus the eight above
dotnet $CSC -nologo -langversion:latest -t:library -nostdlib+ -noconfig -unsafe+ \
  -out:PisAndDag.dll $REFS @srcs.txt
```

`dotnet build` does **not** work — NuGet restore is blocked by the proxy. Call `csc.dll`
directly; it needs no restore.

**Clean baseline is 0 errors, 14 warnings** (unused fields, obsolete vanilla calls, one
unreachable line — all pre-existing). Anything above that is yours.

**The uploads copy of the mod goes stale.** Files staged earlier in a session will be older
than the device. If the build reports errors in files you never touched (`PADWorld` missing
`MileOfferings`, `PADKeybinds` missing `Wind`, `MorsPresence` missing `State`), the staged
copy is old — re-stage those files, do not "fix" them.

**A parse-only check**, when a full build is overkill: same `csc` call with no references,
then grep for `error CS1[0-9]{3}`. Binding errors flood, syntax errors surface. Verified to
catch an injected stray brace as CS1022.

---

## 12.10 Verified this session (tML 1.4.4, checked against the DLL or the decompile)

**Draw order and culling**
- `Main.DrawNPCs` iterates **slot 199 → 0**. `NPCLoader.PreDraw` returning false skips the
  sprite but `PostDraw` still runs.
- Vanilla culls an NPC more than **800 px** outside the screen rect; its `PreDraw` never runs.
- `Main.DrawProj_Inner_DoDrawProj` calls `ProjectileLoader.PreDraw` then `PostDraw`; same
  shape.
- Plain NPC draw position: centred on X, sat on `position.Y + height + 4 − frameHeight/2`,
  flipped when `spriteDirection == 1`.

**Hit path**
- `Update_NPCCollision` → `CombinedHooks.CanNPCHitPlayer` → immunity check (`player.immune`,
  or `hurtCooldowns[slot]` when the slot is ≥ 0) → `Player.Hurt` → `NPCLoader.OnHitPlayer`.
  The immunity check happens **before** any mod hook, so an immune player never reaches
  `OnHitPlayer` at all.
- `ImmunityCooldownID` members are exactly: `General`, `TileContactDamage`, `Bosses`,
  `DD2OgreKnockback`, `WrongBugNet`, `Lava`.
- `Player.Hurt` sets `immuneTime` 20/40 (80 with `longInvince`) for slot −1, or writes
  `hurtCooldowns[slot]`.

**Movement**
- `Player.Update` order: `SetControls` → … → `Update_NPCCollision` → `PostUpdateRunSpeeds`
  → `HorizontalMovement` → `JumpMovement` → wings → `PreUpdateMovement` → `DryCollision` →
  `PostUpdate`.
- `DryCollision` splits motion into 16 px steps when `velocity.Length() > 16` and returns
  the accumulated result. See §12.4 bug 3.
- `maxFallSpeed` is re-clamped every tick in several places.

**Video** (asked about cutscenes)
- FNA ships `Microsoft.Xna.Framework.Media.VideoPlayer` — fully public: ctor, `Play(Video)`,
  `Stop`, `Pause`, `Resume`, `GetTexture()`, `IsLooped`, `Volume`, `PlayPosition`, `State`.
- `Video`'s constructors are `internal`, **but `Video.FromUriEXT(Uri, GraphicsDevice)` is
  public and static** — that is the way in, no content pipeline.
- Backend is `Theorafile`, so the format is Ogg Theora:
  `ffmpeg -i in.mp4 -c:v libtheora -q:v 7 -c:a libvorbis out.ogv`.
- Catch: `FromUriEXT` needs a file on disk, and mod assets live inside the `.tmod` —
  `Mod.GetFileBytes` to a temp path first. `GetTexture()` must be called on the main thread.

**Other**
- `ModBiome`: `Music => 0` is silence, not "no override". Filter opacity fades over ~1 s;
  `ScreenShaderData.UseIntensity`, `uOpacity` is the fade.
- `BackgroundTextureLoader.GetBackgroundSlot(Mod, path)` — any path containing
  `"Backgrounds/"` autoloads. Far and middle draw at 2× from the same top; close's `ref b`
  is a flat Y offset.
- `NPC.oldPosition` is the pre-move position for that tick (the engine does
  `oldPosition = position; position += velocity`), which is what makes swept tests exact.
- Boss Checklist `customPortrait` is `Action<SpriteBatch, Rectangle, Color>`.

---

## 12.11 Errors hit this session and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| Body missing at boss arrival | `BossArrivalWait` held boss-flagged segments | `NPC.PAD().OwnIntro = true` on body and tail |
| Critters not consumed | Source is `EntitySource_Parent`, not `ItemUse` | One `is EntitySource_Parent` test (§12.6) |
| CS0101 duplicate types | User saved chat attachments into the folder as `-1.cs` | Real files updated; `-1.cs` stubbed to a one-line comment. **The user must not save attachments — files are already written by `device_commit_files`.** |
| CS1519 invalid token `}` | A stray brace left by deleting a method with text surgery | Removed; and §12.9 exists now so this cannot ship again |
| Whip felt like a bar / exploded | See §12.3 | Angular spring chain |
| Ragdoll never fired | Three separate causes | See §12.4 |
| Fill pass skipped every column | `AshTop` scan window started below the ash | Scan from `y = 10`; log added |
| White blobs instead of soft smoke | Straight alpha in a premultiplied renderer | §12.7 |
| Outline read as rings per segment | Each segment drew its glow before its own sprite | One silhouette from the first-drawn part (§12.5) |

---

## 12.12 Open TODO after session 3

**Needs the user's hands (no delete tool in that session):**

1. Delete `Content/NPCs/Abomanation_Small/Abomanation-1.cs`, `AbomanationBody-1.cs`,
   `AbomanationHead-1.cs`, `AbomanationTail-1.cs`. All four are 81-byte comment stubs.
2. Delete `Content/Sounds/AbomanationWarn.ogg` — superseded by `CCBypassWarn.ogg`, same
   audio, and nothing references the old name.
3. Still outstanding from session 2: `ChlorophyteBullet.cs` + `.png`.

**Decisions parked:**

4. `BossTitle` on `AbomanationHead` is currently *"The starved hundred who merged into one."*
   Discussed alternatives: drop the second clause (the player is looking at the merge), or
   switch to the victim-class axis — **"Farmers, mostly."** for the juvenile and
   **"They ran out of farmers."** for the post-Moon Lord one, where the second rewrites the
   first on sight. User's call.
5. The planted spray move, `PAD_NPC_Plan.md` §2.4 — designed, **explicitly not to be
   implemented until the user says so**.

**Built but not yet play-tested:** the whole-animal silhouette, the soft star, the bounce
physics at `WhipTossBoost 4`.

**Designed since, in the plans rather than here:**

- `PAD_Item_Plan.md` **§4 — the magic memory system.** Memorised spells and incantations,
  one pool of ten, cast through a staff or a seal held in hand, cycled with two keybinds
  that skip what the held catalyst cannot cast. **Nothing of it exists**: no magic item, no
  summon item, no catalyst, no save data. Multi-session build, and §4.6 is the order.
  *[Superseded in session 4: built. See `PAD_Item_Plan.md` §4.7.]*
- `PAD_NPC_Plan.md` **§2.8 — the Abomanation's drops.** A material plus recipes rather than
  a random weapon pick. Gaping Jaw, the whip, a hand minion, a bow of teeth, the lore
  number. The two incantations are held back until the memory system exists.
- `Terraria_Mod_Lore_Bible_ver_1.1.md` **§XXIV — where magic comes from.** [LOCKED] Not
  primordial, not divine: drawn out of the land and the Shimmer beneath it. **The elements
  are places**, which is why the element list looks unrelated — it was surveyed, not
  designed. Read this before writing any spell, element or catalyst.

**Next thing discussed:** a cutscene system — **not** video. Frame sequences drawn as a
shadow-puppet play: flat silhouettes, two or three poses each, a keyframe table of
`{tick, who, where, pose}`, played over the black screen `LegendsScreenSystem` already
fades. The conceit is that a play is *somebody's version*, so it can be wrong, which gives
the Bible the contradictory accounts it asks for — a player who watches a tidy puppet of
the Abomanation and later meets the real one notices the difference and is never told.
Start with one scene and three figures.

---

## 12.13 Process notes

- **Compile before delivering. Always.** §12.9. Two errors reached the user in one session
  because "it looks right" is not a check.
- **Re-stage before every edit**, and `cmp` the staged copy against what was last delivered.
  The user edits between turns; two of this session's edits were the user's own.
- **Measure motion in Python before shipping it.** Every whip design was decided by a
  fifty-line simulation, including the one that proved the rope unfixable.
- **Read the IL when behaviour contradicts the API.** All three ragdoll bugs were invisible
  in the signatures and obvious in `Player.Hurt` and `Player.DryCollision`.
- **When the user says a feature "does not work", check the layer below the obvious one.**
  "Wings are immune to the ragdoll" was immunity frames. Nothing to do with wings.

---

# SESSION 4

> **The log starts late.** The magic system (spells, incantations, catalysts, the Magic
> Arsenal, the Makeshift Altar, the mana potion effects) was built over rounds that were
> not written up here. What exists is recorded in **`PAD_Item_Plan.md` §4.7**; read that
> first. This section covers the last round in detail and what it taught.
>
> Where session 4 disagrees with 1–3, session 4 wins.

---

## 13.1 What landed

| Work | Where | Notes |
|---|---|---|
| `/magicarsenal`, `max`, `normal` | `MagicArsenalCommand.cs`, `MagicArsenal.Maxed` / `OpenSlots` / `Max()` / `Normal()` | Test command. `Maxed` is saved as `"maxed"`. The UI reads `OpenSlots`. |
| Conquest element and its ramp stroke | `MagicColours.cs` | First element whose stroke is a ramp. Item Plan §4.7. |
| Aquatic Firearms | `Spell/AquaticFirearms.cs`, `Projectiles/Magic/AquaticFirearmsBattery.cs`, `AquaticFirearmsSkin.cs` | No source yet. Damage changed by the user to gun + ball (was ×2/3). |
| Chlorophyte, Arm Me + Armed | `Incantation/ChlorophyteArmMe.cs`, `Buffs/Armed.cs`, `Projectiles/Magic/ArmMeCall.cs` | Witch Doctor, after Plantera. |
| Fix: Armed turned nothing | `ArmedRounds.PostSetupContent` | §13.4. |

Localization added: `Items.AquaticFirearms`, `Items.ChlorophyteArmMe`, `Buffs.Armed`,
`Projectiles.AquaticFirearmsBattery`, `Projectiles.ArmMeCall`. Placeholder rows added to
`PlaceholderTextures.md` for `AquaticFirearms.png`, `ChlorophyteArmMe.png`, `Armed.png`.

---

## 13.2 The compiler, as it stands

§12.9 still holds, with two changes:

- The script (`build.sh`, recreate it per §12.9 in a new container) compiles with
  **`-define:CLIENT`** added.
- **Clean baseline is now 0 errors, 13 warnings.** Anything above that is yours.

---

## 13.3 Verified this session (tML 1.4.4, against the DLL IL)

- `On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float`
  hooks the real implementation; the `Vector2` overload forwards to it. One hook sees every
  projectile made.
- In `NewProjectile`, `owner == -1` means `Main.myPlayer`. Do the same substitution before
  reading `Main.player[owner]` in a hook.
- `ApplyStatsFromSource`: a parent **projectile** passes on its `CritChance`; a player
  **item** source adds `GetWeaponCrit`. A bullet fired from a projectile does not get the
  gun's crit by itself: add it after spawning.
- `EntitySource_ItemUse_WithAmmo` → `EntitySource_ItemUse` → `EntitySource_Parent`.
  `Projectile.GetSource_FromThis()` is an `EntitySource_Parent`. One `is EntitySource_Parent`
  test covers both (as §12.6 found for critters).
- `PostSetupContent` runs after `ContentSamples.Initialize`: `ContentSamples.ItemsByType`
  is safe to read there, modded items included.
- `ItemID.Count` 5456, `ProjectileID.Count` 1022.
- Mod PNGs load as raw images with **straight** alpha; vanilla XNB textures are
  **premultiplied**. Reading a vanilla sprite's pixels (`AquaticFirearmsSkin`) must allow
  for it.
- `ChatManager`'s text shadow is four offsets (±x, ±y) at spread 2; it draws text a word at
  a time; `DrawColorCodedString` applies `ChatManager.WaveColor` to the fill.
- `DynamicSpriteFont` exposes `SpriteCharacters` and `DefaultCharacterData` publicly;
  `SpriteCharacterData` has public fields `Texture`, `Glyph`, `Padding`, `Kerning`. Enough
  to lay text out exactly as vanilla does and draw it by hand.
- Vanilla gun numbers (damage / shoot speed / use time / crit / sound): Minishark 6 / 7 / 8 /
  0 / Item11, Megashark 25 / 10 / 7 / 0 / Item11, S.D.M.G. 85 / 12 / 5 / +10 / Item40.
  PSItem makes the use times 6 / 6 / 4. Musket Ball 7 damage, 4 speed, 2 knockback. Scatter
  is `rand(-40, 40) × 0.01` per axis, `× 0.005` for the S.D.M.G.
- The sixteen vanilla bullet ammo and their projectiles (rarity): Musket Ball, Tungsten
  Bullet 14 (0); Endless Musket Pouch 14 (2); Meteor Shot 36 (1); Silver Bullet 981 (0);
  Crystal 89, Cursed 104, High Velocity 242, Ichor 279, Venom 283, Party 284, Nano 285,
  Exploding 286, Golden 287 (all 3); Chlorophyte 207 (7); Luminite 638 (9).

---

## 13.4 Errors hit this session and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| Armed on, every bullet stays normal | **LuiAFK Reborn** clones each vanilla bullet as Unlimited ammo with the same projectile and `rare = 10`. Taking a projectile's rarity from *all* ammo made every kind rarer than chlorophyte, so the lesser set was empty. | A projectile's rarity comes only from the ammo that **defines** it: vanilla ammo for a vanilla bullet, its own mod's ammo for a modded one. Simulated: old rule `[]`, new rule the 12 expected. The set is now logged at load. |
| Waves would flip sides if the player turned mid-cast | Drawn from the live `player.direction` | Directions frozen at cast (`outDir`, `backDir`) |
| One-off sounds could be lost or doubled | Keyed to exact ages; a skipped or repeated tick misses or repeats | `Once(bit, age, at)` on a `played` bitmask with a 6-tick late window |
| A doc comment claimed a meaning for Conquest | Invented, not in the Bible | Removed. **Never write lore into a comment that the Bible does not say.** |

How the LuiAFK fault was found: `client.log` showed a clean load, the hook registered and
no exceptions, so the fault was in logic, not wiring. Then LuiAFK's `.tmod` was extracted
and its DLL read (workshop folder `steamapps/workshop/content/1281930`, access granted).

---

## 13.5 Open TODO after session 4

**Needs the user's hands (no delete tool):**

1. Delete `Content/NPCs/Legends/SlimeMass.cs`, `Content/Sounds/AbomanationWarn.ogg`,
   `Content/Projectiles/Ranged/ChlorophyteBullet.cs` + `.png`, and the `Content/Tiles/SealGlass.cs` stub.

**Unfinished:**

2. **Aquatic Firearms has no source.** Red rarity; it has to be cheated in.
3. **Reliquary Key has no source.**
4. **No incantation can be cast before Skeletron**: the only seal (Clothier's Chatelaine)
   comes from the Clothier and Skeletron, while Meagre Flame drops from Fire Imps before
   him.
5. **Nothing raises the arsenal past 3 slots** except `/magicarsenal max`.
6. The doc comments in `AquaticFirearmsBattery.cs` (class summary and `GunStats.ShotBase`)
   still say ×2/3; the code is gun + ball.
7. Placeholder art for everything new: `PlaceholderTextures.md`.

**Not yet confirmed in game:** Arm Me's conversion after the fix.

---

## 13.6 Process notes

- **When a feature "does nothing", read `client.log` first.** It told us the hook loaded
  and nothing threw, which ruled out half the causes before any code was read.
- **Suspect the other mods in the user's list.** Anything that scans all content
  (`ContentSamples`, every item, every projectile) sees their content too. Read their DLL
  rather than guess.
- **Log what a load-time rule decided.** One `Mod.Logger.Info` line turns the next
  "it does not work" into a fact.
- **Simulate a rule against the real data before shipping it**, as §12.13 says for motion.
