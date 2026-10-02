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
> **Sessions: 0–10 = session 1, §11 = session 2, §12 = session 3, §13 = session 4,
> §14 = session 5, §15 = session 6.** Later wins. The newest open TODO is §15.8.
> *[And §16 = session 7. Its open TODO is §16.11; the sections after it (§16.13 on) each end
> with what they left open.]*

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

*[Later, session 7: the head is three files (`AbomanationHead.cs`, `.Moves.cs`, `.Show.cs`) and
what it throws is in `AbomanationSpew.cs`. See §16.23.]*

**Chain links live in `ai[]`, not in fields**, because `ai[]` rides the NPC sync packet:
`ai[0]` = sprite mirrored, `ai[1]` = index ahead + 1, `ai[2]` = head index + 1, `ai[3]` =
place in chain. `localAI[0..2]` hold the whip's per-link angle, spin and serial — local
only, never synced, which is fine because the swing is deterministic from synced state.

**Phases** (`phase` field, synced): −1 arrive, 0 chase, 1 dive, 2 rush, 3 fall, 4 whip
run-in, 5 loop, 6 crack.
*[Added to since: 7 the end; and in session 7, 8 the puke's approach and rise, 9 its choke, 10
its heaves, 11 getting to where it lies down, 12 the crawl, 13 lying there, 14 going back under.
See §16.23.]*

**Second wind** at `RagePoint = 0.4` of max life: speed ×1.2, whip every rush with a 4 s gap
instead of 1-in-3 with a 20 s gap, one low roar.
*[Session 7: the whip follows any finished move, not only a rush; the moves come quicker; it
bleeds and glows; and it has the vent. See §16.23.]*

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

---

# SESSION 5

> Bosses in Legends (the Wall, Skeletron, the Queen Bee), the arrival and dialogue UI,
> the level dash, and the first summoner weapon with a whip base class. Long, and cut
> by two context compactions and one dropped link to the computer, so the earliest
> rounds are summarised rather than told in full. The code's own doc comments are the
> fuller account; every class named here opens with one.
>
> Where session 5 disagrees with 1–4, session 5 wins.

---

## 14.1 What landed

| Work | Where | Notes |
|---|---|---|
| The Wall of Flesh, Legends | `Content/NPCs/Legends/LegendsWall.cs` (`LegendsWall`, `LegendsWallStats`, `LegendsWallLaser`, `WallGrabPlayer`, `WallTentacleDraw`, `WallGrabOverlay`) | Life ×1.12, damage ×1.5, walks ×1.1. Behind it in the underworld is death. At 40% it roars and four tentacles come out of the mouth (walk ×1.15): a lash every ~4 s takes whoever it touches into the mouth for 6 s, 45 life/s out of them, 200/s into the Wall (cap 40%). Reach `MaxReach` 448, sway `Sway` 0.7 (both were cut to 7/10 by request). The lash tell plays the CCBypass warning and glows the tentacle (`CCBypass.Warn(Func<Vector2>)`). |
| Skeletron, Legends | `LegendsSkeletron.cs` (`LegendsSkeletron`, `LegendsSkeletronHand`, `SkeletronBreathSkull`, `SkeletronArrival`) | Life ×1.5, damage ×1.2. Four moves, never the same one twice running: Clap, Smash (hands rise 180 px apart, touch, and come down together), Breath (jaw drops, six skulls: `SkeletronHead.png` + `SkeletronJaw.png`), Dive (the head dives at any angle along a see-through path lerping red to orange; `GroundWave.Strike` for walls and ceilings). In every world he touches nothing while being introduced. |
| King Slime's hops | `LegendsSlimes.cs` | More life in the hop. Earlier rounds; see the file. |
| Guide's hint before Legends | `GuideHints.cs`, hjson `GuideHut` | The hut on the trail seaward. The user's own text. |
| Speaker portraits | `Content/UI/DialoguePortrait.cs` (+ `Dialogue.cs`, `TalkOptions.cs`, `PugreamTalk.cs`, `MorsTalk.cs`) | The speaker's head in the top-left corner of every dialogue box. Pitch black until the speaker has said their own name to the player (`DialogueIntroductions`, per world). Town NPCs are always named. |
| Boss plate: a boss that names itself | `BossPlate.NamesItself(type)` / `NameByHand` | For an own-intro boss whose cutscene calls `BossIntro.PlayNameOnly` itself. Queen Bee uses it. |
| The Queen Bee | `Content/NPCs/Legends/LegendsQueenBee.cs`, `QueenBeeGround.cs`, `HoneyGlint.cs`, `Particles/HoneyMote.cs`, `HoneyRipple.cs` | See §14.2. |
| Level dashes | `Content/PADplayer/LevelDash.cs` | While any dash is underway (`Player.dashDelay < 0`), no fall: Tabi, Shield of Cthulhu, Solar and the rest go level and drop when spent. All worlds. |
| Base whip | `Content/Projectiles/Base/BaseWhip.cs` | See §14.3. |
| Joint Arms of the Defiled | `Content/Items/Summoner/JointArmsOfTheDefiled.cs` (+ `JointArmsStreak`), `Projectiles/Summoner/JointArmsOfTheDefiledProj.cs`, `Buffs/ManyHands.cs` | See §14.4. Amalgaermis's first weapon drop. |

Localization added: `GuideHut`, `SkeletronBreathSkull`, `Items.JointArmsOfTheDefiled` (with
`Streak`, `Charged`), `Buffs.ManyHands`, `Projectiles.JointArmsOfTheDefiledProj`. Placeholder
rows added for the Wall's tentacle sprites, Skeletron's head and jaw, the whip's icon and
sheet, and the Many Hands icon.

---

## 14.2 The Queen Bee

**All worlds.**

- *Arrival.* The camera goes to the nearest honey (in 90×60 tiles). If there is none, the
  summon digs a pool beside the player (`QueenBeeGround.BuildPool`): 32 tiles of honey in
  rows 10/8/8/6, a hive bucket with a flat 3-tile hive rim each side, only in plain ground
  with open air above. She comes up out of it dripping, flies level with the player
  (faint gold), shakes herself, roars as her name comes up (`NamesItself`), and the camera
  walks home. No honey and nowhere to dig: she flies in from vanilla's spawn.
- *Death.* Her death is refused (`CheckDead`). She goes to the nearest flat hive
  (`FindNest`, 150×90 tiles; any flat ground if no hive), settles, strains three times,
  and leaves a larva (`PlaceLarva`). Then she swells and bursts, with gores 303–308 thrown
  out in a ring (`Explode`). The server then sets life to 0, calls `checkDead` and sends
  `SyncNPC`: that is when the loot drops.
- *Larva.* Only a pickaxe breaks it. `LarvaWardSystem` turns off `tileCut` and
  `tileObsidianKill` for tile 231, `LarvaWard.CanExplode` refuses explosions, and a whole
  larva ignores losing its ground (`TileFrame` returns false). Once one piece has been
  mined, vanilla takes over and the Queen comes as before.

**Legends.**

- Life ×1, body ×1.3, stingers ×1.75 (bees vanilla), size ×0.75.
- `Charge` is vanilla's charge state rewritten. She lines up ×1.5 faster (pushes ×1.5²,
  speed cap ×1.5), dashes twice as far past the player, and lets out 5–10 bees per dash.
- At 35% she cries out, goes to the nearest honey (180×110 tiles, or builds a pool),
  plunges in and comes up gold. From then on she has +15 flat defense
  (`ModifyIncomingHit`), regains 20 life/s up to 35%, and wears a pulsing glow with bands
  climbing her and a light-gold glint (`HoneyGlint`).

**How it is built.** One `BeeScene` state machine run by the server and synced in
`SendExtraAI` (scene, timer, spot, nest, level side, three flags). Clients adopt a packet
only when the scene changed or the clock drifted more than 6 ticks. During a scene she is
untouchable and harmless (`Hush`).

**HoneyGlint** draws without a shader. In `Main.OnPreDraw` it renders streaks into a
frame-sized render target, then draws her frame over it with a blend that keeps
`destination × source alpha`. That leaves the glint cut to her shape, which `PreDraw`
adds over her.

---

## 14.3 `BaseWhip` — how to make a whip

ExampleMod's `ExampleWhipProjectile` turned into an abstract base (house style:
sealed hooks, `Safe*` virtuals).

1. Projectile: `class X : BaseWhip`. Override what differs:
   - The numbers: `Segments` (20), `RangeMultiplier` (1), `MultihitKeep` (0.8, the damage
     kept per extra enemy hit), `TagBuff`/`TagTicks`, and optionally `ChargeTicks` (the
     example's hold-to-lengthen; the item then needs `channel`).
   - The sheet: `Frame(i, count)`, `Origin(...)`, `Scale(...)`. `i` 0 is the handle and
     `count - 2` is the tip. The defaults are the example's 10-wide sheet.
   - The hooks: `OnWhipHit(target, hit, damage, first)` (owner only), `OnCrack(tip)` (all
     machines), `OnCrackEnd(landed)` (owner only). Drawing: `DrawWhip`, using
     `DrawLine`/`DrawPieces(points, colour, offset)`.
2. Item: `Item.DefaultToWhip(type, damage, knockback, shootSpeed, useTime)` and
   `MeleePrefix() => true`.
3. The ai slots: `ai[0]` is vanilla's clock, `ai[1]` the charge, `ai[2]` is free (sent in
   the spawn packet).

**Reach** is `shootSpeed × the item sample's useAnimation × 2 × RangeMultiplier`, so a
slower swing reaches further on the same settings. To keep a length, scale
`RangeMultiplier` by `oldUseTime / newUseTime`. Firecracker is 1.4 at 30 ticks with
shootSpeed 4.

---

## 14.4 Joint Arms of the Defiled

- 30 damage, **35 use time** (the user's change from 25), knockback 2, Green, 1g 50s.
  `RangeMultiplier` is `1.4 × 30 / 35` = Firecracker's length. No tag. `MultihitKeep` 0.8.
- **Streak** (`JointArmsStreak`, the Chlorophyte Rifle's rules). A crack that hits anything
  counts 1. A crack that hits nothing, 210 ticks without a hit, putting the whip away or
  dying resets it. At 5 it is charged. The next crack spends it in `Shoot`, sends
  `ai[2] = 1` so it glows red and bursts at the crack, and gives **Many Hands: +50% summon
  damage for 11 s** (the user's change from 10% / 16 s). Whips inherit Summon damage in
  full, so the whip gains too. The spending crack counts toward the next streak if it lands.
- Every hit spatters blood on Amalgaermis's own scale (`Spatter`, `Abomanation.Blood*`).
- **Drop:** guaranteed. `PerPlayerDrop` in classic, in `AmalgaermisBag` in expert/master.
  `PAD_NPC_Plan.md` §2.8 row marked built.
- Tooltip uses `{$CommonItemTooltip.Whips}` (vanilla's "Your summons will focus struck
  enemies"). Flavour: *'The end of it is the worst of it.'*
- Art is placeholder: the tip is the animal's tail end (the user's "anus tip").

---

## 14.5 Verified this session (tML 1.4.4, against the DLL IL)

- **`NPC.StrikeNPC` runs `HitEffect`, then the hit sound, then `checkDead`.** A boss whose
  death `CheckDead` refuses still plays vanilla's death `HitEffect` first (gores, 50 dust).
  Cut it at the source with `On_NPC.VanillaHitEffect` (it exists in TerrariaHooks): run
  `orig` with `life` set to 1 for that call. Done in `LegendsQueenBee.HitNotKilled`.
- `checkDead`: `NPCLoader.CheckDead` runs before `DeathSound`, `NPCLoot` and
  `active = false`. The only things before it are For the Worthy extras for types 13,
  36 and the hornets.
- `Main.OnPreDraw` is invoked inside `DoDraw`, after `DrawToMap`, `InitTargets` and
  `UpdateDisplaySettings`, and **before** the backbuffer `Clear(Black)`. It is safe for
  your own render targets; restore the previous targets afterwards.
- **`BlendState.Additive` is SourceAlpha/One.** A `Color × float` scales alpha too, so the
  colour is squared (half strength adds a quarter). For linear adding, use a
  `BlendState` of One/One.
- Whips:
  - `Projectile.WhipSettings` is a struct field with `Segments` and `RangeMultiplier`.
  - `FillWhipControlPoints(proj, list)` takes two parameters in 1.4.4.
  - The crack is at `ai[0] == (int)(timeToFlyOut / 2)`; the projectile is killed at
    `ai[0] >= timeToFlyOut`.
  - Whips use neither `ai[1]` nor `ai[2]`, and `ai[2]` is in the `SyncProjectile`
    packet.
  - `Item.DefaultToWhip` sets `autoReuse = false`.
  - Vanilla range multipliers: Leather 0.75, Spinal Tap 0.95, Snapthorn 1, Firecracker
    1.4, Cool Whip 1.5, Durendal 1.75 (20 segments), Morning Star 1.6 (40), Dark Harvest
    2.15 (30), Kaleidoscope 2.2 (40).
  - Vanilla multi-hit damage kept per hit: Leather 0.5, Snapthorn 0.6, Firecracker 0.66,
    Cool 0.7, Durendal 0.8, Kaleidoscope 0.8, Dark Harvest 0.9, Spinal Tap 0.9, Morning
    Star 0.95. Modded whips must do it themselves in `OnHitNPC`.
  - Firecracker: 37 dmg, 2 kb, speed 4, 30 ticks, rarity 4, sells 3g. Spinal Tap: 27, 2,
    5, 30, Green, 1g 50s.
- `SummonMeleeSpeedDamageClass` inherits Generic and Summon in full, and only attack speed
  from Melee.
- `ModProjectile.OnKill` is the hook to use (`Kill` is still there).
  `ILocalizedModType.GetLocalization(suffix)` works with one argument.
- Queen Bee (aiStyle 43): state `ai[0] == 2` flies above the player, then goes to 1
  (bees). The charge is `ai[0] == 0` (odd `ai[1]` = dashing). The sheet is 172×1824, 12
  frames of 152 (0–3 dash, 4–11 hover), picked by `localAI[0]`.
- Larva (tile 231): `tileCut` and `tileObsidianKill` are true in vanilla. Its
  TileObjectData has `LavaDeath = false`.
- `Player.dashDelay < 0` for as long as a dash is underway. `PreUpdateMovement` runs after
  gravity and before movement.
- `NPC.HitModifiers.Defense` is a `StatModifier`; add to `.Flat`.
- **tModLoader rewrites the hjson when it builds** and may drop quotes (`Streak: "Streak:
  {0}/{1}"` came back unquoted, which is still valid). Re-stage it before every edit.

---

## 14.6 Errors hit this session and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| Queen Bee's honey glint barely visible | `BlendState.Additive` squared the streak strength | One/One blend; strengths 0.65 / 0.5. Checked with a numpy model of the real blend maths before shipping |
| Killing blow threw her death gores, then she flew to the nest intact | `HitEffect` runs before `CheckDead` | `On_NPC.VanillaHitEffect` detour (§14.5) |
| Her scenes stalled after Emerge and after Swell | Missing scene transitions | `EmergeDone`; Swell → Burst |
| Camera never went home after her death | `watching` reset every tick | `DeathShot` up to `HomeAt`, then `PanHome` |
| Clients never saw her die in multiplayer | Life set to 0 without a strike | `SyncNPC` after the server's `checkDead` |
| `SendUserFile` returned 500 for some files | Upload side | `device_commit_files` with `stagedPath` under `/mnt/user-data/outputs/` works as the fallback |

---

## 14.7 Open TODO after session 5

**Needs the user's hands (no delete tool):**

1. Delete `Effects/LegendsShader.fx` and `Effects/LegendsShader.xnb`.
2. §13.5's list, where not yet done.

**Not yet confirmed in game.** None of this session's work has been tested in game by me.
The riskiest parts:

- the Queen Bee's scenes in multiplayer, and the pool digging in odd terrain;
- the whip's streak and charged crack;
- the level dash with every dash accessory and mount;
- the refused-death hit effect.

**Unfinished:**

3. Amalgaermis's other drops (§2.8 of `PAD_NPC_Plan.md`): Gaping Jaw, the hand minion,
   the tooth bow, the number, trophy and mask.
4. Placeholder art (`PlaceholderTextures.md`).

---

## 14.8 Process notes

- **Re-stage before every edit, even files you delivered an hour ago.** The user edits
  between rounds (this session: the whip's numbers, straight into the files) and tModLoader
  rewrites the hjson on build.
- **When a link drops mid-request, answer what was asked first.** The user's messages
  survive; read them all before acting. "make it 50%" meant the buff.
- **Model the render maths, not just the picture.** The glint mockup looked right; the
  blend state in code did something else. A numpy model of the exact blend equations
  caught it.
- **Trace a boss's death through `StrikeNPC` → `HitEffect` → `checkDead`** before refusing
  a death: anything vanilla shows on death has already happened by the time `CheckDead`
  is asked.

---

# SESSION 6

> A spear (Visus), the Kingdom of Grace as a biome with its own light, and the
> Flamethrower's fire redrawn. Two small tools came out of it that later work should reuse:
> `Strip` (a bending ribbon of triangles) and `Easing` (a motion in pieces). Cut by one
> context compaction, after the code was written and before it was delivered.
>
> **§14 (session 5) was written last round but never reached the computer**: the copy
> there stayed at session 4. It is delivered now, with this. See §15.7.
>
> Where session 6 disagrees with 1–5, session 6 wins.

---

## 15.1 What landed

| Work | Where | Notes |
|---|---|---|
| Visus | `Content/Items/Melee/Visus.cs` (+ `VisusDrop`, `VisusBag`), `Projectiles/Melee/VisusHold.cs` (`VisusGrip`, `VisusPose`, `VisusLayer`), `VisusThrust.cs`, `VisusChop.cs`, `Buffs/VisusShut.cs` (removed later, see the note at the top of §15.2) | §15.2. Eye of Cthulhu drop. |
| The Kingdom of Grace, a biome | `Content/Biomes/GraceBiome.cs`, `GraceLight.cs`, `Particles/GraceMote.cs`, `Effects/Grace.fx`, `PisAndDag.LoadGraceFilter` | §15.3. **`Grace.fx` must be compiled by hand.** `GraceBuilder.WardFloor` went from `private` to `internal` for it. |
| Flamethrower and Elf Melter flames | `Content/Projectiles/Ranged/FlamethrowerFlames.cs` (+ `FlameBillow.png`), `Particles/FlameSmoke.cs` | §15.4. Look only; damage and behaviour are vanilla's. |
| `Strip` | `Content/Graphics/Strip.cs` (+ `Smear.png`, `SoftLine.png`, `Streak.png`) | §15.5. |
| `Easing` | `Content/Projectiles/Base/Easing.cs` | §15.5. |
| Flesh Maiden (added at the end of the session) | `Content/Items/Melee/FleshMaiden.cs` (+ `FleshMaidenState`), `Projectiles/Melee/FleshMaidenStrike.cs` (+ `FleshMaidenGrip`), `FleshMaidenSmash.cs`; `VisusHold.cs` changed to draw it | §15.10. |

Localization added: `Items.Visus`, `Buffs.VisusShut`, `Projectiles.VisusThrust`,
`Projectiles.VisusChop`, `Biomes.GraceBiome`. Placeholder rows added for `Visus.png`,
`VisusShut.png` (the item overlay), `VisusSpear.png`, `VisusSpearGlow.png`, the Shut Eye
icon, and the biome's bestiary icon and background.

---

## 15.2 Visus

> **CHANGED at the end of session 6; this note wins over the bullets below.** The user's
> "takes 60 seconds" meant ticks. The blow now **takes 60 ticks** (`Visus.ChopTicks`: the
> multiplier is 60 / 45, which comes to exactly 60 at base speed, checked by running tML's
> `CombinedHooks` maths on .NET 8), and the next blow **waits 120 ticks from the end of the
> last** (`VisusPose.Rest`: set in `VisusChop.OnKill`, counted down in `PostUpdate`, read by
> `CanUseItem`). **Shut Eye is gone**, as the user asked ("delete things that are excess"):
> the buff and its icon and localization, the inventory overlay (`Items/Melee/VisusShut.png`),
> the glint and note when the blow was ready again, and the "once a minute" tooltip line.
> The eye still shuts at the end of the blow and stays shut while it rests, half open for the
> last 10 ticks, open when the blow is ready (`VisusPose.RestingEye`, seen during a thrust).
> **Later still:** the blow does **triple** damage (`Visus.ChopDamage` 3), not double.

- **Numbers.** 52 melee damage, 45-tick use, knockback 6.5, Green, sells 1g, no auto reuse,
  `noMelee` + `noUseGraphic` (the projectiles are the weapon). One at a time: `CanUseItem`
  refuses while a thrust or a blow of the owner's is still out.
- **Left click, the thrust** (`VisusThrust`). Drawn back, driven out, held, drawn in, as
  shares of the use: back by 0.16, out by 0.40, held to 0.58, hits 0.20–0.62. The front hand
  slides along the shaft (`Grip`: 56 → 64 → 14 texels from the butt), so the point reaches
  112 px (7 tiles) from the centre on a level thrust. Hits along the line from the front
  hand to the point, 14 px thick, each enemy once a thrust (`localNPCHitCooldown = -1`),
  `ownerHitCheck`, cuts grass.
- **The step** (`Lunge`, owner only): 32 px × |cos angle| over 0.12–0.60 of the thrust,
  eased, capped at 4 px/tick, measured from where the player actually went (so walls and
  slopes are honoured), and it only ever raises forward speed. Ground friction is added back
  because the game takes it off before the player moves. Skipped mounted or grappling.
- **Right click, the blow** (`VisusChop`). ×2 damage, ×1.3 use time, ×1.5 knockback, then
  **Shut Eye** (`VisusShut`: 3600 ticks, debuff, persists through death, the Nurse cannot
  remove it). The swing is four `Easing` pieces in degrees: lift −25 → −120, hang to −130,
  down to +60 (`PolyIn` degree 3, so the weight is at the bottom), settle to +66. The ground
  stops it: from the start of the downswing the hammer's teeth (6 px bitten in) and the point
  are tested against solid tiles; the first tick one is in, the contact angle is bisected
  8 times between last tick and this, and the head stays there: sound, a 6/14 screen shake,
  `GroundWave.Strike`, tile dust, sparks; it stops hitting 2 ticks later. The first enemy
  hit stalls it 5 ticks. It hits along its whole sweep since last tick (tested at four
  angles from last tick's to this tick's), so nothing is stepped over at the bottom, where
  it moves a tile a tick.
- **The eye.** Three frames (open, half, shut) and a glow sheet (added light, drawn only
  over the open frame). In a thrust it stays as it rests, with a glint as the point
  arrives. In the blow it opens and glows crimson through the swing and shuts at the end
  (Shut Eye has begun). When Shut Eye runs out it opens over 21 ticks, with a cold glint and
  a low `MaxMana` note if Visus is in hand (`VisusPose`). The inventory icon has
  `VisusShut.png` drawn over it meanwhile.
- **How it is drawn.** `VisusLayer`, a `PlayerDrawLayer` between `SolarShield` and
  `ArmOverItem` (vanilla's slot for a held item behind the front arm), while
  `VisusPose.Held`. The projectiles only draw what trails behind (`PreDraw`, which runs
  before the player is drawn): the thrust's streak, the blow's three shadows and its smear
  (`Strip`: a dark band, crimson light, a blood edge, a hot core).
- **Poses are written once, "upright"** (`VisusGrip`): facing right, y down, from
  `RotatedRelativePoint(MountedCenter)`. World = (x·direction, y·gravDir); world angle =
  (a or π−a), negated under reversed gravity; composite arm rotation = (angle − π/2)·gravDir;
  the sprite flips vertically when (direction < 0) XOR (gravDir < 0). The shaft sits on
  half the frame height (`Axis` 18), so a flipped frame keeps its origin. The back hand
  finds where the back arm can reach the shaft (`BackArmOnto`: 48-step root search, then
  16 bisections).
- **Drop.** Classic: `PerPlayerDrop` under `Conditions.NotExpert`. Expert and master: the
  Eye's treasure bag (`ItemDropRule.Common`).

---

## 15.3 The Kingdom of Grace's light

**Where.** `GraceBiome.Covers(x, y)`: extra large world, the plain's span
(`Layout.Plain`), no deeper than 4 rows under the plain's level. The level is found again
from the saved `"Grace.Court"` mark: its bottom minus `GraceBuilder.WardFloor` is plan row
0, the ground line. Marks are saved and sent by `GraceWorld`, so it works on clients.
The Undercity, the Crypt, the fissure and the Font are below it and keep their dark.

**The filter** (`"PisAndDag:Grace"`, pass `GracePass`, ps_3_0). Registered in
`PisAndDag.LoadGraceFilter` through `RequestAssetIfExists("Effects/Grace")`; if the .xnb is
missing it logs one warning and `GraceBiome.FilterReady` stays false, so nothing breaks.
`SpecialVisuals` → `ManageSpecialBiomeVisuals`; the filter manager fades `uOpacity` over a
second in and out. What it does, in order: a grade (blue sky to hazy gold, warmer,
highlights toward gold, shadows lifted to brown, saturation 1.1; weaker at night), the sun
(the light map below: haze in drifting bands where the sun reaches, and what it falls on
brightened), a glow off the brightest things (two rings of 8 taps, no loops), a warm glare
where vanilla draws the sun, a warm vignette.

**The light map** (`GraceLight`, client only, nothing saved). Every frame the filter is in
use (`PostDrawTiles`, which is before the filter is drawn), one texel a tile over the view,
8 tiles round it and 44 more toward the sun, sides rounded up to 32. Light is carried from
the sun's side a row at a time (a column at a time when the sun is low), each row blending
the two tiles it comes from. A solid block stops it, glass (tile 54) lets it through, a
platform or table 0.72, anything else placed 0.92, water most of it. **A glass wall with no
block in front of it is a window: a source of its own colour** (×0.85), so halls get
slanting shafts, colonnades bars, towers shadows. Two textures alternate so one is never
written while bound. Parameters: `uColor` the sun's colour, `uSecondaryColor` (sun x, sun y
on screen, day 0–1), `uDirection` toward the sun, image 1 the map, `uImageOffset` the map's
top-left in world pixels from the view's top-left (bottom edge under reversed gravity).

**Without the shader** the plain still changes: `ModifySunLightColor` warms the tile light
(×1.08, 0.96, 0.74, fading in over a second on the plain; the sky is left alone, the grade
does it) and `GraceMote`s hang where `Lit` says the sun reaches (walked through the tiles,
no map needed).

**Sun model.** Day = smoothstep in over the first 7% of the day and out over the last 7%.
Tilt from −1.15 to +1.15 rad across the day, rising on the left. Colour (1, .86, .56) at
noon to (1, .60, .28) low, by the square of how low.

---

## 15.4 The flames

`FlamethrowerFlames : GlobalProjectile` on `ProjectileID.Flames` (Flamethrower and Elf
Melter). `PreAI` returns false and keeps vanilla's clock exactly (`localAI[0]++`, die at 72,
velocity ×0.95 from 60); vanilla's `AI_193_Flamethrower` does nothing else but throw dust,
which is what this replaces (§15.6). Damage, hitbox, reach and debuffs are untouched.

Drawn as a row of billows back along each burst's streak (vanilla's streak length), each
given the fuel's age there: size, colour ramp (blue root, white-yellow, orange, red edge),
rise, soot. Four layers: soot (covers), outer flame and inner flame (mostly add, cover a
little so the colour holds against a bright sky), and a hot core while young. Placement,
turn and noise frame come from a hash of the flame's identity, so every machine draws the
same fire and it does not boil. `ai[0] == 1` (the cold flame) is drawn blue. Embers, soot
puffs (`FlameSmoke`: dark, drawn over rather than added, carries on then rises), a nozzle
flare, and sparks on hit. All textures premultiplied white, four 64 px frames.

---

## 15.5 Two tools to reuse

**`Strip`** (`Content/Graphics/Strip.cs`): a band that bends, which a sprite batch cannot
draw. `Strip.Begin()`, then `Strip.Pair(first, second, firstColour, secondColour, u)` for
each step along the band (world pixels; v 0 on the first edge, 1 on the second), then
`Strip.Draw(texture, sampler)` **with no sprite batch open**: End the batch before, Begin it
again after with the game's own settings. It is a `BasicEffect` (texture × vertex colour)
through `Main.GameViewMatrix.TransformationMatrix`, so it lands where a sprite would, zoom
and reversed gravity included. Premultiplied blend: alpha 0 adds light, alpha darkens. The
vertex array grows as needed.

**`Easing`** (`Content/Projectiles/Base/Easing.cs`): `Easing.At(progress, Piece[])`. A
`Piece(ease, start, from, shift, degree)` runs from `start` in the progress until the next
piece starts, going from `from` to `from + shift`. Eases: Linear, Sine In/Out/InOut, Poly
In/Out/InOut (t to the degree), ExpOut. Same idea as Calamity's `CurveSegment` +
`PiecewiseAnimation`, own code. Not named `Curve`: FNA already has
`Microsoft.Xna.Framework.Curve`.

---

## 15.6 Verified this session (tML 1.4.4, against the DLL IL)

- **`ScreenShaderData.Apply`** sets `uColor`, `uOpacity` (= `CombinedOpacity`),
  `uSecondaryColor`, `uTime` (= `Main.GlobalTimeWrappedHourly`), `uScreenResolution`
  (= screen size / `GameViewMatrix.Zoom`, i.e. the view in world pixels), `uScreenPosition`,
  `uTargetPosition`, `uImageOffset`, `uIntensity`, `uProgress`, `uDirection`, `uZoom`; then
  binds each `UseImage` texture to device slot `index + 1` and sets `uImageSize{index+1}`.
  Pass the sampler to `UseImage` yourself; otherwise it is picked by whether the size is a
  power of two.
- The view's top-left in the world is `screenPosition + screenSize × 0.5 × (1 − 1/zoom)`.
  A pixel's world offset from it is `coords × uScreenResolution`.
- `AI_193_Flamethrower`: `localAI[0]++`, `Kill` at 60 + 12, `velocity × 0.95` from 60, then
  dust and nothing else.
- FNA defines `Microsoft.Xna.Framework.Curve`: a class of that name clashes in any file
  using that namespace.
- The particle handler's additive bucket is `BlendState.Additive` (SourceAlpha, One): its
  textures stay straight white. Everything drawn under `AlphaBlend` wants premultiplied
  textures (tML loads mod PNGs straight, §13.3).
- `PlayerDrawLayers.SolarShield` → `ArmOverItem` is the slot for something held behind the
  front arm. Draw a held weapon there, not from the projectile, and its trail from the
  projectile's `PreDraw`.
- In `Main.DoDraw`: `FilterManager.BeginCapture`, … `DoDraw_WallsTilesNPCs`,
  `SystemLoader.PostDrawTiles`, `DrawProjectiles`, `DrawPlayers_AfterProjectiles`, …
  `FilterManager.EndCapture` (where the filters are applied). So filter parameters set in
  `PostDrawTiles` are used the same frame, and a projectile's `PreDraw` is behind the
  player. `ModifySunLightColor` is called from `DoDraw` too (`ApplyColorOfTheSkiesToTiles`).
- `Filter.IsInUse()`, `Filter.GetShader()`, `ScreenShaderData.UseImage(Texture2D, int,
  SamplerState)`, `UseImageOffset`, `UseDirection`, `UseSecondaryColor(float, float, float)`
  exist as used.
- **Use time.** `CombinedHooks.TotalUseTime` = max(1, (int)(useTime × player mult × item mult
  ÷ use speed)), truncated, not rounded. `TotalAnimationTime` is the same for the animation,
  with the speed-adjusted use time folded in. A multiplier that should land on a whole number
  can truncate one short: run it before trusting it (45 × (60f / 45f) is exactly 60 on .NET 8).
- `ItemCheck_Inner` turns `altFunctionUse` 1 into 2 **before** `ItemCheck_CheckCanUse`, so
  `CanUseItem` sees 2 on a right click.
- `ModProjectile.OnKill(int timeLeft)` runs on each machine where the projectile is killed,
  whether it ran out or was cut short.

---

## 15.7 Errors hit this session and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| Visus's 2-px shaft came out black | An auto-outline pass outlined the whole shaft | Parts drawn explicitly |
| The blow came to rest at the wrong angle on flat ground | Which point of the head was tested against the ground | The teeth less a 6 px bite, and the point; the first correction went the wrong way, so check the rest angle in the preview |
| Build error on `Curve` | FNA's `Microsoft.Xna.Framework.Curve` | Renamed to `Easing` |
| Glass blocks stopped the sun in the preview | The built kingdom has glass as tiles (54) as well as walls | Glass tiles let it through, tinted |
| The sky went mint and grey | The warm grade on a blue sky gives mint; a sky already warmed by `ModifySunLightColor` reads as grey to the shader's sky test | Blue sky turned to gold in the shader; the background colour is no longer touched |
| Halls had no shafts | Back-wall windows face the viewer, not the sun | Windows are light sources in the map |
| `FilterReady` never assigned (warning) | Filter not registered yet | `LoadGraceFilter` |
| No way to compile `.fx` here | vkd3d-compiler 1.2 has no HLSL front end | `Grace.fx` written conservatively for ps_3_0 (explicit taps, no `[unroll]`, a local float before `.xxx`). **It has not been through a compiler.** |
| King Slime (Legends) shook side to side harder and harder and threw the player off the world; the log had `IndexOutOfRangeException` in `Player.FloorVisuals`, `Player.UpdateBiomes`, `Main.UpdateAudio_DecideOnNewMusic` and `NPC.SpawnNPC`, all at once | His slam could start while he still held the player (a grab lasts 3–5 s, and once he is hurt his wind-ups are short enough for three jumps and the crouch to fit inside it). The climb aims over the target's head and follows them, and a held target is pinned to his middle: vertically he rose ~10× too high, sideways the grab's lag (twice his sideways speed) made the aim feed on itself, doubling any half-pixel stray every tick (the drain heals him, his size follows his health, `Entity.Center` rounds an odd width down). Simulated: off the world 15–25 ticks after the nudge. Vanilla then read the tile under the player past the end of the tile array | Neither special (slam or teleport) starts while he holds anyone (`LegendsKingSlime.HoldingAnyone`); it waits for the first jump after he lets go |
| Session 5 missing from the handoff on the computer | The committed copy was the session 4 one (device mtime 8 s after session 5 was written, size unchanged) | Re-delivered with this section. **After committing a document, list the folder and check the size.** |

---

## 15.8 Open TODO after session 6

**Needs the user's hands:**

1. **Compile `Effects/Grace.fx` with EasyXnb (`Effects/Compiler`) into `Effects/Grace.xnb`.**
   Until then the plain only gets the warm tile light and the motes. If it will not
   compile, send the compiler's error.
2. Delete `Effects/LegendsShader.fx` and `Effects/LegendsShader.xnb` (still there).
3. Delete `Content/Buffs/VisusShut.cs`, `Content/Buffs/VisusShut.png` and
   `Content/Items/Melee/VisusShut.png` (Shut Eye, removed; nothing uses them, and the mod
   builds with or without them).
4. §14.7 and §13.5, where not yet done.

**Not yet confirmed in game.** Nothing from this session has run. Riskiest:

- `Grace.fx` itself, and the light map lining up with the world under zoom and reversed
  gravity;
- Visus's step in multiplayer, and the blow's ground stop on slopes, platforms and
  half-blocks;
- the flames against a bright sky and in the dark (designed against a model of the blend
  sums, not seen in game).

**Unfinished:** placeholder art for everything new (`PlaceholderTextures.md`).

---

## 15.9 Process notes

- **Emulate a look before shipping it.** The Grace grade and light map were run in Python
  over renders of the generated kingdom (the structure harness) first; that is what found
  the mint sky and the missing shafts. Visus's poses, streak and smear were checked the
  same way (`Claude outputs/visus_preview.gif`, `grace_light_compare.png`).
- **Held weapons are player layers, trails are projectile draws.** The layer puts the spear
  between the back arm and the front one; a projectile's `PreDraw` is behind the player,
  which is where a trail belongs.
- **Write a pose once, in the player's own frame**, and map it to the world in one place
  (`VisusGrip.ToWorld`/`ToWorldAngle`). Facing and gravity then cost nothing.
- **Check the computer after every commit of a document.** §15.7.

---

## 15.10 The Flesh Maiden (added at the end of the session)

Visus grown over. Made from Visus and 13 Essence of the Fouled at a Demon or Crimson Altar
(`TileID.DemonAltar` covers both): after Amalgaermis, before the Wall.

- **Numbers.** 47 melee damage, ~~30 ticks~~ **40 ticks** (50 for the smash, §15.10.1),
  knockback 6, Orange, sells 2g, **no auto reuse** (the user's choice: one click a step; kept
  in the rework as a nerf). `noMelee` + `noUseGraphic`; the projectiles are the weapon.
  ~~It never moves the player.~~ The combo never moves the player; the smash throws them back
  (§15.10.1).
- **Left click, the combo** (`FleshMaidenStrike`, ai[0] the step, ~~30~~ 40 ticks each): a swing
  down, a swing up, then a thrust. The shape is Calamity's Biting Embrace (Broken Biome Blade:
  `SwingMode` 0 a small up-to-down slash, 1 a large down-to-up slash, 2 a thrust; a `Combo`
  counter on the weapon, reset after about 50 idle ticks). Ours is its own code: the step comes
  from `FleshMaidenState.NextStep()` in `Shoot`, and the combo starts over after 60 ticks
  without a strike or smash out (`Idle`, zeroed by each of them every tick).
  - **Swings.** An arc laid about the aim, the aim held within ±46° of level: a wind-up, then
    a sweep on a steep ease-out (`PolyOut`, degree 3), then a settle. Down: ~~−55 → −78, → +74,
    → +62~~ −34 → −48, → +44, → +36 degrees; up: ~~+50 → +74, → −74, → −62~~ +32 → +44, → −48,
    → −40 (about 90° each, §15.10.1). The swing down stops on the ground (the blade's edge less
    a 6 px bite, or the point, in a solid tile; the angle halved out as Visus's blow does) and
    comes back up ~~14°~~ 10° off it over 8 ticks. The swing up starts from the ground when
    its wind-up would be in it (`floor`), and throws dirt as it tears up. **The swing up is
    drawn turned over on its shaft**, so the blade leads both swings.
  - **The thrust.** Visus's timing; the grip goes 66 → 78 → 12 texels from the butt, the point
    160 px (10 tiles) out when level.
  - **Hits.** The shaft past the hand (16 px wide) and the whole head (40 px wide), over 6
    samples of the sweep since last tick; each enemy once a strike; the first hit of a swing
    holds it still 3 ticks.
- **Right click, the smash** (`FleshMaidenSmash`): VisusChop's swing fitted into ~~30~~ 50 ticks
  (top .30, down .42, through .62, hits to .70), ~~×2~~ ×2.5 damage, ×1.5 knockback, the same ground
  stop, a 4-tick stall on the first hit, 8 sweep samples. The next smash waits **60 ticks from
  the end of the last** (`FleshMaidenState.Rest`, set in `OnKill`).
- **The eye** is Visus's: shut while the smash rests, half open over the last 10 ticks.
- **Blood** is Amalgaermis's (`Abomanation.Blood`, and Joint Arms of the Defiled's spatter
  maths, `FleshMaidenGrip.Bleed`).
- **Shared with Visus: the drawing in the hands.** `VisusLayer` now draws a `PolearmArt`
  (sheet, glow, length, frame height: `PolearmArt.Visus`, `PolearmArt.FleshMaiden`), handed
  over by `VisusGrip.Hold(..., art, turned)`; `VisusPose.Turned` flips the sheet over the
  shaft. Visus passes neither and draws as before.
- **Previewed** by emulating the code in Python on flat ground (`Claude outputs/flesh_maiden_preview.gif`):
  the swing down meets the floor at about level, blade first; the swing up starts lying on
  it; the thrust reaches 159.8 px; the smash lands at about level. *[Previewed again after the
  rework, with the throw: §15.10.1.]*
- **Art** is placeholder: `FleshMaiden.png` (96x96), `FleshMaidenSpear.png` (168x144, three
  eye frames), `FleshMaidenSpearGlow.png` (`PlaceholderTextures.md` has the points the code
  reads).
- Flavour: *'The eye has company now.'*

**Not yet confirmed in game:** all of it; most of all the turned-over swing up under reversed
gravity and facing left, and the ground stops on slopes and platforms.

### 15.10.1 Reworked (same session, after the first delivery)

The user: smaller swings, 40 ticks, no auto reuse; the smash on right click, about 50 ticks,
2.5 times the damage, and throwing its wielder back 2 tiles up and 10 back as it lands,
spilling blood. Asked and answered: smaller arcs (about 90°, not raised as high; the thrust
still aims wherever you point), and the throw on impact.

- **Timing.** `UseTicks` 40. The smash is `SmashTicks` 50 through
  `UseTimeMultiplier`/`UseAnimationMultiplier` = 1.25 when `altFunctionUse == 2` (40 × 1.25 is
  exactly 50; melee speed shortens both, tML's own rounding: 45 at ×1.1, 40 at ×1.25).
  `Shoot` still hands each projectile `itemAnimationMax` as its length.
- **Arcs.** The tables above; the swing up's lowest point is a named constant (`UpLowest`
  44), used for its ground test too. On flat ground with a level aim the floor takes the
  lower half, so each swing shows about 49° (the blade's edge meets the floor at +0.6°).
- **The throw** (`FleshMaidenState.Throw`, called by the smash's `ThrowBack`): the moment the
  smash meets the ground, or at `ThroughAt` if nothing is under it, once a smash. The wielder
  goes 2 tiles up and 10 back, away from the side they face; a burst of 14 droplets, then 2 a
  tick for 26 ticks (`Spill`). The smash stops hitting 2 ticks after the throw and stops
  looking for the ground. It runs on every machine the smash runs on (everyone sees the
  blood); only the owner's copy moves the player.
  - **Up** is one push worked out a tick at a time as `Player.Update` does it: gravity is
    added before the move (IL 0x60dc, every tick for a normal player, standing too), so
    v = (g + √(g² + 8gh)) / 2 with the size of this tick's gravity (Featherfall leaves the
    field signed by `gravDir`). At g 0.4: 5.264 px/tick, peak 32.03 px, down again after
    25.3 ticks.
  - **Back** is steered in `PreUpdateMovement` (IL 0x8207: after vanilla's run, friction and
    gravity, before the collisions) by how far the player really went last tick, toward
    `throwSpeed × ticks` (160 / 25.3 = 6.32 px/tick), never past 160, at most 1.5 × that to
    catch up. It stops dead at 160 px, at a wall (under 0.25 px gained in a tick), after 90
    ticks, or when something else has the player (`Held`: dead, frozen/webbed/stoned, hook,
    rope, mount, the Wall's tongue). Walk and jump are held off (`SetControls`) meanwhile.
  - **No stop on landing, on purpose.** Brought down short of 160 (a low roof, rising ground)
    the player skids the rest of the way, so the 10 tiles hold wherever there is room. An
    earlier draft stopped on `velocity.Y == 0` in `PreUpdateMovement`, which never fires
    there: gravity has already been added by then.
  - **Checked** in .NET floats with vanilla's collision (lands flush, TileCollision returns
    the gap; a ceiling bump leaves velocity.Y 0.01, IL 0x8495): flat ground, either facing
    and under reversed gravity, peak 32.03 px, lands on tick 26 at exactly 160 px;
    Featherfall peak 32.02, lands tick 44 at 160; a 3-tile tunnel comes down at 44 px and
    skids to 160 by tick 27.
- **Tooltip:** "Right click smashes it down for 2.5 times the damage" / "The smash throws you
  back" (was "... for double damage").
- **Previewed** (`Claude outputs/flesh_maiden_preview.gif`, `work/flesh/flesh_anim.py`, now
  with a `Thrower` that mirrors the throw): the swing down meets the floor on tick 12 of 40;
  the thrust reaches 160 px; the smash lands on tick 30 of 50 and the throw carries the
  player to the 10-tile mark.

**Not yet confirmed in game:** the throw in multiplayer, and in water or honey (the move is
slowed there and the steering makes it up).

---

# SESSION 7

> Beneath Grace (the underground of the Kingdom of Grace as a biome of its own, with its own
> grade and its own water), and the Queen Slime remade in the King's manner: her gel alive,
> her arrival out of the Gelatin Crystal, her wings, her death, and two moves of Legends. The
> same conversation as the end of session 6 (the Flesh Maiden rework, §15.10.1), after a
> second context compaction; written and delivered in one piece.
>
> Where session 7 disagrees with 1–6, session 7 wins.

---

## 16.1 What landed

| Work | Where | Notes |
|---|---|---|
| Beneath Grace, a biome | `Content/Biomes/GraceDeepBiome.cs` (+ `GraceDeepAir`), `GraceDeepWater.cs` (`GraceDeepWaterStyle`, `GraceDeepWaterfallStyle`, `GraceDeepSplash`, `GraceDeepDroplet`), `Effects/GraceDeep.fx`, `PisAndDag.LoadGraceDeepFilter` | §16.2. **`GraceDeep.fx` must be compiled by hand.** `GraceBiome.Below` went from `private` to `internal` for it. |
| The Queen Slime, every world | `Content/NPCs/Legends/LegendsQueenSlime.cs` (+ `LegendsQueenSlimeShots`, `LegendsQueenSlimeSystem`), `.Look.cs`, `.Rite.cs`, `.Wings.cs`, `.Death.cs`, `QueenSlimeArt.cs`, the art in `Content/NPCs/Legends/QueenSlime/`, `Content/Particles/QueenShard.cs` | §16.3 to §16.6. |
| The Queen Slime, Legends | `LegendsQueenSlime.Moves.cs`, `QueenSlimeGrab.cs` (a `ModPlayer`), `Content/Projectiles/NPCs/QueenSlimeCore.cs` | §16.7. |

Localization added: `Biomes.GraceDeepBiome` (*Beneath Grace*, *the halls beneath Grace*),
`Projectiles.QueenSlimeCore` (*Crystal Core*). Placeholder rows added for the biome's icon and
background, the wings, the shards, the crystal's cracks, and the four white textures of her
arrival and death.

Previews: `Claude outputs/queen_slime_preview.gif` (the arrival, the wings, the two moves, the
death, one after the other).

---

## 16.2 Beneath Grace

- **Where.** `GraceDeepBiome.Covers(x, y)`: a Grand world (`ExtraLargeWorld.Active`), over the
  plain's span (`Layout.Plain`), deeper than `GraceBiome.PlainLevel + GraceBiome.Below` (the
  plain's own biome ends 4 tiles under its level), and no lower than `GraceDeepBiome.Bottom`:
  the bottom of `GraceWorld.Area` (the guard map's rectangle, saved and synced), or on a world
  without one, the bottom of the lowest mark the builders left. The cellars, the harbour basin,
  the Undercity, the Reliquary, the fissure, the Font and the Rootworks round the Shimmer.
  `Priority` is `BiomeHigh`.
- **The grade** (`Effects/GraceDeep.fx`, pass `GraceDeepPass`, filter
  `"PisAndDag:GraceDeep"`, `EffectPriority.High`; colour gold (1, 0.8, 0.48), secondary blue
  (0.22, 0.42, 0.8)): the dark sunk into sapphire, cool and deep; the middle tones a fifth
  desaturated; what is lit turned gold, a little more saturated, and glowing (three rings of
  taps round each pixel, gold); a soft S-curve; a vignette, blue at the edges; faint bands of
  blue haze fixed to the world, in the dark only. Loaded with `RequestAssetIfExists`: without
  the `.xnb` there is no grade and a warning in the log, nothing worse
  (`GraceDeepBiome.FilterReady`).
- **The water**, clear and mint (118, 227, 194). Vanilla's own water textures recoloured and
  made see-through (0.40 to 0.70 opaque, premultiplied: the base 0.40, the ripples and streaks
  0.46, the light 0.56, the surface line 0.70), where vanilla's are opaque and drawn at 0.6.
  `LightColorMultiplier` 0.93, 0.99, 0.97 (vanilla 0.88, 0.96, 1.015). Its own falls, splash
  dust (`UpdateType = DustID.Water`) and ceiling drips (`UpdateType = GoreID.WaterDrip`,
  `LiquidDroplet`, 15 frames).
  - **Why a hook as well as `ModBiome.WaterStyle`.** `Main.CalculateWaterStyle` takes a
    fountain first, then a blood moon's night (style 9), then *the surface background's own
    water* for background styles 1 to 8, and only after all that asks the modded styles
    (`WaterStylesLoader.ChooseStyle`). The plain runs to the sea, whose background picks one.
    So `GraceDeepAir` detours `On_Main.CalculateWaterStyle`: while the local player is Beneath
    Grace, Grace's water, unless a water fountain on screen says otherwise.
- **The motes.** Two tries a tick at 0.18: a `GraceMote`, gold (255, 206, 128) or one in five
  pale mint (165, 236, 212), in open air with no liquid, only where `Lighting.Brightness` is 0.1
  or more, brighter with it. None in the dark.
- **Art.** The water textures are vanilla's recoloured (generator
  `work/queen/water/make_water.py`); the bestiary icon and background are placeholders.

---

## 16.3 The Queen Slime: her gel alive (every world)

`LegendsQueenSlime` (a `GlobalNPC` for `NPCID.QueenSlimeBoss`, partial across six files) does
for her what `LegendsKingSlime` does for the King. Vanilla's AI (`AI_121`) fights her the rest
of the time, untouched; a scene or a move of hers takes the tick instead (`PreAI` false) and
hands her back at rest (`Release`: `ai[0..2]` zeroed, vanilla picks its next move in its own
time).

- **Her shape** (`.Look.cs`, `Animate`, every client, nothing sent): springs as the King's.
  Coiled low and wide as vanilla's tells run out (the hop's countdown, the slam's wait, the gel's
  gathering, read off vanilla's own `ai[1]`); tall the moment she leaves the ground, gel thrown
  off her base; drawn out by her speed in the air; flattened by how hard she lands, and the
  crown hops off her head as she comes back up; a slow breath in flight. A **ripple**: her body
  is drawn in 14 slices, and a wave runs up them after every landing and every blow (each slice
  swells and shifts as it passes), with a trace of it always there.
- **A fifth see-through** (`Opacity` 0.8): her body and her wings. Her core and crown are whole.
- **Drawn as vanilla draws her body**: an immediate batch (`AlphaBlend`, `PointClamp`,
  `DepthStencilState.Default`, `CullNone`), `GameShaders.Misc["QueenSlime"].Apply()` **with no
  draw data** (vanilla passes null: `uSourceRect` is then (0, 0, 4, 4)), her drawn, the game's
  deferred batch back, then `Main.pixelShader.CurrentTechnique.Passes[0].Apply()`. The slices
  are drawn from the same sheet, so the shader sees each of her pixels as it would have. Core
  (`Extra_186`) and crown (`Extra_177`) where vanilla puts them, riding her shape: vanilla's
  per-frame offsets are `QueenSlimeArt.CoreRise`/`CrownRise` (they differ only at frame 6).
- **Her slam** is given its full weight: stretched on the drop, and on the landing that ends it
  (`slamming`, remembered from the tick before, since vanilla has already reset `ai[0]` on the
  landing tick) the whole of her weight, the dust sheet, the ground heaved (`GroundWave.Slam`)
  and the screen shaken, without a second copy of vanilla's sound.
- **Her gel** as particles: `SlimeGlob` in her three colours (`QueenSlimeArt.Gel`) off her
  launches, landings and drips; vanilla's tinted slime dust (`DustID.TintableDust` with
  `NPC.AI_121_QueenSlime_GetDustColor()`).
- **Her afterimages** in the slam's drop and the dash, through the same shader.
- **Screen shake** from anything of hers is scaled by distance (`Near`: all of it within 700 px,
  none past 2100).

---

## 16.4 The arrival (`.Rite.cs`, every world)

On `OnSpawn` (server; the summoner from `EntitySource_BossSpawn.Target`) she is hidden where
she will form and the rite starts. Ticks:

| Step | Ticks | What |
|---|---|---|
| RISE | 70 | The Gelatin Crystal (`Item_4988`) flies up out of the hand that held it, on an arc (a quadratic curve 60 px over the higher end), turning twice and slowing to upright, growing to 3×; the camera goes with it and leans in to 1.75 |
| CRACK | 120 | It hangs, bobbing; cracks at 24, 58 and 92 (`QueenSlimeCrystalCracks.png`, each stage over the last), each a jolt of 14 ticks, a crystal impact, splinters and sparks; the light out of it grows; after the third it trembles more and more. Lean creeps to 1.9 |
| BURST | 24 | A flash of pink, shattering, splinters and a ball of light; what is left is its shape in white (`QueenSlimeMorph.png` frame 0) in a glow of her colours |
| MELT | 48 | The white shape runs, weightless and wobbling like something liquid, into the shape of her core (morph frames 0 to 15, 3× to 1.5×) |
| GLOW | 120 | It hangs, breathing, light drawn into it; a slow pulse; a breath in at the end |
| GROW | 54 | She swells out of it, white (`QueenSlimeWhite.png`) with her colours round her (`QueenSlimeHalo.png`): a damped spring from 0.08 of her size, past it to about 1.12, and back; her colours come into her from a third of the way; the white core fades into her own; gel thrown off her and pulled back in. The camera snaps out to 1.35 |
| CROWN | 36 | Her crown falls 150 px onto her head, turning, and lands at 18 with a clink and a glint; she gives under it |
| FALL | until she lands | Gravity; a heavy landing (her slam's sound, the dust, the ground heaved) |
| ROAR | 90 | `Cinematic.Roar` (pitch +0.2) with a wet sound; her name comes up (`BossIntro.PlayNameOnly`, the plain plate, zoom 1.15); gel shaken off her top |
| HOLD | 41 | `PlainPlateTicks − PlainLook − RoarTicks + 5`, as the King's and the Queen Bee's. Then the fight, the camera walking home over 60 |

*[Later in session 7: her bar no longer shows through the rite; it comes up with her name at
ROAR (`BossPlate.BarWaitsForName`). And every tick of the rite, anything hostile within 3000 px
with less than 500 life is killed and gives nothing (`ClearScene`). See §16.14.]*

- **Where.** `FindRiteSpot`: ground to the summoner's front at 300, 240, 360, 180, 420, 120 or
  480 px (then the other side), within 240 px of their feet, with a 132 px wide column clear
  from over her crown to the ground; her core forms 190 px over it. None (a tunnel): over the
  summoner, as high as the room allows.
- **The shot** is the King's: letterbox, interface away, the player held and made immune, for
  anyone within 3000 px; leans as a framing that counts the player's own zoom.
- **Her opening pause** after it is one second, not vanilla's 160 ticks: `EndRite` sets
  `localAI[0]` to her life (vanilla's own marker that its first tick is done, and where it
  would set `ai[1] = −100`).
- **Glows are tints.** Every glow of hers (the crystal's, the white shapes', the one behind her,
  her core's red) is three tints of her colours, widest faintest (`DrawGlow`: 0.3, 0.5, 0.8 of
  the strength, as colour *and* alpha), and a little added light. Added light alone went white
  on the Hallow's day sky and hid the shapes (§16.10).

---

## 16.5 The wings (`.Wings.cs`, every world)

At half her life, before vanilla's second half starts (`PreAI`, the server's). Untouchable and
harmless throughout. A teleport half done is undone first (`Resize` to 1, `hide` off).

| Step | Ticks | What |
|---|---|---|
| LAND | up to 90 | Down to the ground if she is not on it |
| CROUCH | 34 | Lower and wider, trembling; a low pulse |
| SPROUT | 16 | Her wings come out of her back folded (frame 0) and shoot up: from inside her (34 px down) and a quarter size, past full size, and back; gel burst off her back |
| SPREAD | 40 | They open (frames 1 to 5 over 22 ticks) and grow to 1.25×: a gust both ways along the ground, streaks of air, a jolt, a flash of her colour |
| TAKEOFF | 64 | Wings up for 8, then the first beat hard and fast and a push of 12 px/tick up, dying at 0.93 a tick; the screen shaken, and if she was standing, the ground heaved (`GroundWave.Slam`), a ring of dust, and dust that rises with her for 36 ticks; the wings settle to their flight size |

*[Superseded later in session 7: SPROUT, SPREAD and TAKEOFF are retimed and made stronger, and the flap is a new bent wingbeat. See §16.13.]*

*[Later in session 7: once her wings are out she never has more than half her life, or vanilla
would put her back in its first half with her wings still on. See §16.14.]*

- **The wings are remade** (`QueenSlimeWings.png`, `work/queen/art/wings.py`): feathered, a
  leading edge of coverts, primaries, secondaries and tertials, tips in her colours; the right
  wing, the left mirrored (origin mirrored by hand: `(344 − 22, 222)`, since a flip mirrors the
  texture inside the same quad). Flight size 0.8: each reaches about 235 px from its root, so
  with her they span about three times her width. They tip with her rise and fall, beat faster
  the faster she flies, and carry a trace of her colours going round.
  *[Superseded later in session 7: the flap (frames 6 to 11, one straight wing turned about its
  root) is replaced by `QueenSlimeWingbeat.png` and the beat in `.Beat.cs`. See §16.13.]*
- Vanilla's wings (`Extra_185`) are no longer drawn (her `PreDraw` returns false).

---

## 16.6 The death (`.Death.cs`, every world)

`CheckDead` refuses her first death (life pinned at 1, untouchable) and starts this; the blow
that killed her is shown as one she lived through (`HitNotKilled`, a detour of
`NPC.VanillaHitEffect`, the Queen Bee's pattern). At the end the server zeroes her life and
calls `checkDead` again, and that one is let through (`ending`), the loot with it.

| Step | Ticks | What |
|---|---|---|
| SPASM | 72 | A roar gone wrong (`Cinematic.Roar`, 0.6, pitch +0.5); she convulses where she hangs (random kicks to her shape, jerks off true, a ripple always running, gel thrown, wet sounds), wings beating at nothing |
| FALL | until she lands | Her wings break into splinters (`QueenShard`, 40 a wing, along their length; sparks, a shatter) and she drops; a heavy landing |
| FRENZY | 900 | She throws herself about: 60% of throws at the player (where they will be, their speed × 24), the rest within 190 px of her; 70 to 280 px high; worked out to land where aimed (NPC gravity 0.3, fastest fall 10); 2 to 24 ticks between. **She hurts while she does** (contact, and vanilla's slam shockwave, `QueenSlimeSmash`, 40, where each lands) |
| STILL | up to 90 | One last landing, and she stays |
| SHUDDER | 180 | Harder and harder, whiter and whiter (`QueenSlimeWhite.png` to 0.8, flickering), glowing; wet sounds closer and higher; letterbox and a small lean for anyone near, nobody held |
| BURST | 30 | Her own death sound (`NPCDeath64`) in an explosion; a flash; gel (70), her dust (150, as vanilla's death), smoke, rolling dust, motes, sparks, three balls of light, the ground heaved; her crown thrown up (`GoreID.QueenSlimeCrown`; vanilla's wing gores are not thrown, her wings being splinters already). **Every slime of hers dies** (`StrikeInstantKill` on 658, 659, 660, the server's). Then her life to 0 and `checkDead`, `DeathSound` null |

*[Superseded later in session 7: FRENZY is 480 (eight seconds). At the BURST her slimes pop and
give nothing (`QueenSlimeMinions.Expire`), as they now do by themselves at ten seconds. See §16.14.]*
*[And later again: thirty seconds, not ten. See §16.15.]*

---

## 16.7 Legends: her numbers and two moves

- **Numbers** (`SetDefaults`, before difficulty scaling): life, contact damage and defense ×1.2;
  her gel and her slam's shockwave ×1.2 (`LegendsQueenSlimeShots`, for 922 and 926 spawned by
  her, `EntitySource_Parent`). Her minions are untouched.
  *[Superseded later in session 7: contact damage and her gel and shockwave are ×1.1; life and
  defense stay ×1.2. See §16.14.]*
- **Picked in place of vanilla's** (`ConsiderMove`, the server's, after her AI): a fresh pick
  from rest (state 0 last tick, 3 to 5 now). The pick after one of hers is always vanilla's.
- **The core** (first half, on the ground, 30% of picks, the player within 1300 px):
  - TELL 50: her core glows red and rises in her (14 px), swelling; red drawn into it; a pulse,
    and a clear note 16 ticks before.
  - OUT: `QueenSlimeCore` (hostile, **200**: the game doubles a hostile projectile's damage
    against a player and the world's difficulty multiplies it, so 400 / 800 / 1200). Its way,
    in its own frame: along = R sin(πu), out = W sin(πu) u, u from 0 to 1; out low, round the
    far end where it slows, back higher, on the side away from the ground. R is 960 (half a
    1920 screen) or 160 past the player, at most 1600; W is R/2. The axis is turned off the
    line to the player by the angle of the point on its way out at their distance (found by
    halving), so the way out passes exactly through where they stood. Its length in ticks is
    πR/26, 70 to 150. The last 15% bends to wherever her core is. Spinning 0.42 rad a tick and
    unsteadily, tumbling (narrower and wider), red (a tinted glow in its own shape, a trail),
    red light, red torch dust, vanilla's boomerang whirr (`Item7`) every 8 ticks. Start, clock
    and axis are all it needs (ai[0] owner, ai[1] axis, ai[2] reach with the side in its
    sign; start and clock sent): every machine flies it alike. The server ends it early if she
    is no longer waiting for it.
    *[Superseded later in session 7: a hit takes exactly 150 from a player (`Damage` 75, and
    `ModifyHitPlayer` sets the final damage), whatever the difficulty. It flies at half the
    speed: πR/13 ticks, 140 to 300. See §16.14.]*
  - CATCH 20: back into her with a gulp; the red goes out of it.
- **The dash** (second half, flying, 35% of picks, within 1500 px):
  - LINE up to 80: to 400 px to the player's side, level with them.
  - TELL 30: draws back 40 px, wings raised, a cry (`ForceRoar`, pitch +0.4), trembling.
  - CHARGE up to 60: across at 22 px/tick, tracking their height a little, wings swept, her
    body drawn out along the way, afterimages, streaks of air. Anyone she touches is taken; past
    them by 400, or out of time, it is over (END 24).
  - HOLD 246: whoever she took is inside her (`QueenSlimeGrab`): pinned at her middle, lagging
    her movement, tipped with it, no controls, no items; **50 a second through any armour**
    (their own client, `Player.Hurt` on the WrongBugNet immunity slot, cleared first, so no other
    immunity stops it and it gives none), four times; **150 life into her a second** (the
    server, at the 60-tick marks, `HealEffect`). She hovers, working at them; drawn over them
    (`DrawHolding`, after the players: her body goes through vanilla's shader, which player draw
    data cannot carry).
    *[Later in session 7: the life into her never takes her past half. See §16.14.]*
  - SPIT 24: out, thrown clear (7 px/tick away, 6 up), 60 ticks' grace.
- The grab is asked for every tick (`Hold`); the tick she stops asking they are out, so a death,
  a despawn or a lost packet cannot leave anyone stuck in her.

---

## 16.8 How her scenes run in multiplayer

- The server (or a single player) decides every scene and move, and each step's start
  (`Go`/`Next`, `netUpdate`); `SendExtraAI` carries the scene, the step, its clock, the rite's
  three places, the side, the last move, who is held, and what she has lost (bits: wings out,
  dying, wings broken, core out). A packet moves a client's clock only on a new step or 6 ticks
  of drift.
- Every machine runs the scene from that. `Entered()` is true once, the first tick a machine
  sees a step (and resets the marks); `Crossed(mark)` is true once as the clock passes a mark,
  wherever a packet moved it. Everything seen or heard is played on every machine; nothing that
  moves her or hurts anyone is decided anywhere but the server.
- **`Restore`**: whatever a scene did to her is undone on every machine the tick it sees the
  scene end or change, the server's by `Release`, a client's by the packet. It also resets the
  step memory, so the same move can start again later.
- `CheckDead` can run on a client that dealt the killing blow itself; its guess is overwritten
  by the server's (`dying` is taken from the packet, not added to).

---

## 16.9 Verified this session (tML 1.4.4, against the DLL IL; where the 1.4.5 decompile was read first, the 1.4.4 IL was checked after)

- `NPC.UpdateNPC` order: `AI()` (PreAI, AI, PostAI; PostAI runs even when PreAI returned
  false), then gravity (0.3, scaled down near the top of the world; `maxFallSpeed` 10, only
  when `!noGravity`; so vanilla's slam never passes 10, whatever its AI asks), then
  collision, then `FindFrame`, trails, net. On the ground, `velocity.Y` is exactly 0 during AI.
- Queen Slime (657): 114×100, damage 60, defense 26, life 18000, `knockBackResist` 0,
  `DeathSound` `NPCDeath64`. Vanilla draws her slam's afterimages from `oldPos`, so her trail
  cache is kept (`NPCID.Sets.TrailingMode` 6 in the 1.4.5 decompile; the 1.4.4 value sits in
  an array blob and was not read). `AI_121`: state 0 picks after 60
  ticks (second half 120); a first tick of `ai[1] = −100` unless `localAI[0]` is set; slam
  (4): wait 30, rise to 60 toward 384 px over the target, hang (30, or 10 in the second half),
  fall; lands to state 0 with `Item167` and projectile 922 (40); gel (5): gather 50, a ring of
  6 (10 in the second half) of 926 (30); minions every 2% (1.5%) of life lost, spawned at the
  end of her AI; half life resets her to state 0. Frames: idle 0–3 every 10 ticks, rising
  4–7 (then 20–23 in the second half), falling 8–10.
- `MiscShaderData.Apply(DrawData? = null)`: with draw data it sets `uSourceRect`,
  `uWorldPosition`, `uImageSize0` from it; without, `uSourceRect` (0, 0, 4, 4). Vanilla draws
  her body with null.
- `Player.Hurt` (the public 10-argument one): `cooldownCounter` −1 tests `immune`; 0 to 3 test
  `hurtCooldowns[counter]`; a hit then sets that slot 40 (80 with a Cross Necklace), not
  general immunity. `ImmunityCooldownID.WrongBugNet` is 3.
- `NPC.StrikeInstantKill()` strikes and, in multiplayer, sends the strike. `NPC.HealEffect`
  broadcasts from the server. `NPCLoader.CheckDead` is called by `checkDead` on any machine.
- `NPC.SpawnBoss` sets `target` and `timeLeft × 20` **after** `NewNPC` (so after `OnSpawn`),
  and touches nothing else.
- `Entity.GetSource_FromAI` is `EntitySource_Parent`. `Gore.NewGore(IEntitySource, ...)`.
  `GoreID.QueenSlimeCrown` 1258, `GoreID.QueenSlimeWing` 1259. `DustID.TintableDust` is 4 (=
  `t_Slime`, her vanilla dust), crystal shards 68/69/70, `RedTorch` 60.
- In `Main.DoDraw`, `DrawPlayers_AfterProjectiles` runs between an `End` and a `Begin`: a
  detour of it draws in a batch of its own.
- Mod PNGs: `ImageIO.ToRaw` clears the colour of fully transparent pixels on load (the wings'
  sheet has palette colours under its transparent pixels; harmless, but any preview must do
  the same).
- `ModBiome` localization: `DisplayName` and `TownNPCDialogueName` (the second is
  `IShoppingBiome.NameKey`).

---

## 16.10 Errors hit this session and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| Build: `MathHelper.Clamp` on ints | XNA's is float only; the float result fed a `Rectangle` | `Math.Clamp` |
| Build: `DrawData` not found | Missing `using Terraria.DataStructures` | Added (then not needed: see the shader row) |
| The wings' sheet at 344×4608 | Over 4096 on a side | Two columns of six (688×2304) |
| The wings read as bones | Joint dots and thin sticks for the arm | A thick leading edge of coverts, no dots, softer feather outlines |
| The cracks did not show | White lines on a pale crystal | Dark lines with light leaking through them |
| In the preview, the burst was a flat white disk and the white shapes lost in white | Glows as added light only, on the Hallow's bright sky (and the preview's own bloom was blended wrong) | Glows as tints plus a little light (`DrawGlow`, `QueenSlimeArt.DrawRed`); the preview blends additive particles as `BlendState.Additive` does |
| A rectangle round each wing in the preview | The preview kept colour under transparent pixels | The preview clears it, as tML does on load |
| Her body shader given her frame as draw data | Assumed the shader needed `uSourceRect` | Applied with none, exactly as vanilla (§16.3) |
| A launch played as she was let fall in her arrival and her death | Hanging with no gravity leaves `velocity.Y` 0, which reads as standing the tick gravity comes back | `velocity.Y = 0.01` as she is let go (vanilla's slam does the same) |
| A scene ended and another started between two packets would leave the first's changes on a client | `Restore` only ran on a scene ending in nothing | `Restore` on any change of scene |

---

## 16.11 Open TODO after session 7

**Needs the user's hands:**

1. **Compile `Effects/GraceDeep.fx` with EasyXnb (`Effects/Compiler`) into
   `Effects/GraceDeep.xnb`.** Until then Beneath Grace has its water and its motes but no
   grade. Its syntax was checked with `glslangValidator` in HLSL mode (§16.12); it has not been
   through fxc.
2. **`VisusHold.cs` still loads the level spear sheets** (`Projectiles/Melee/VisusSpear.png`,
   `VisusSpearGlow.png`, `FleshMaidenSpear.png`, `FleshMaidenSpearGlow.png`), which are now only
   in `Tools/ArtBackup`. A missing asset stops the mod loading (§13.3). Put them back, or take
   those loads out, whichever the diagonal work intends. Not touched here.
   *[Done later in session 7: the user moved to the diagonal sheets; the code draws only them.
   See §16.13.]*
3. §15.8 and earlier, where not yet done.

**Not yet confirmed in game.** Nothing from this session has run. Riskiest:

- the arrival in multiplayer, and in a cramped spot (`FindRiteSpot`'s fallback);
- the dash's grab in multiplayer (the held player's client drains, the server heals), and a
  player let go inside terrain if she held them in a wall (she climbs out of solid ground while
  holding);
- the core's way with the player far above or below her;
- vanilla's shader for her body through slices (the preview could only approximate the shader);
- the death's frenzy on rough ground.

*[Later in session 7: add all of Amalgaermis's new moves, its voice included. See §16.23.]*

**Unfinished:** placeholder art for everything new (`PlaceholderTextures.md`).

---

## 16.12 Process notes

- **A shader's syntax can be checked here.** `glslangValidator` (apt `glslang-tools`) reads
  HLSL: strip the `technique` block, then
  `glslangValidator -D --hlsl-dx9-compatible -e <PassName> -S frag -V file.hlsl -o /dev/null`.
  It catches undeclared names, wrong swizzles and type errors; it is not fxc, and says nothing of
  ps_3_0's instruction limits. `Grace.fx` and `ManaNausea.fx` pass it.
- **A software sprite batch for previews** is in `work/queen/preview/` (`render.py`: premultiplied
  `AlphaBlend`, point sampling, rotation about an origin, per-axis scale, flips, a camera; `fx.py`:
  cut-down particles; `queen.py`: her springs and draw ported). The scene scripts (`arrival.py`,
  `wings.py`, `moves.py`, `death.py`) run the C#'s timings tick by tick. Reuse it for any boss.
- **Glows on a bright sky are tints.** Light added to the Hallow's day sky is white at once; a
  tint of the colour with its alpha shows there and in a cave alike, and a little light on top
  keeps it glowing in the dark.
- **Do what vanilla does, exactly, where vanilla does it**: her shader applied with no draw
  data; the pixel shader put back after the batch is.


---

## 16.13 Later in session 7: the spears on the diagonal, and her wings' beat

**The spears.** The user moved Visus and the Flesh Maiden to the diagonal sheets and put the
level ones (`VisusSpear`, `FleshMaidenSpear` and their glows) in `Tools/ArtBackup`. The mod then
built (0 errors) but would not load: `VisusLayer.Load` and the projectiles' `Texture` still asked
for the level sheets (four `MissingResourceException`s in `client.log`, nothing else).

- `PolearmArt` is now one diagonal sheet: its path, its glow's, the length butt to point, the
  frame size and where the butt is. `Place` always draws it, turned an eighth less than it is
  held (an eighth more when mirrored, the pivot mirrored with it). `DiagonalSheet` is gone.
- `VisusGrip.SpearTexture` and `GlowTexture` are the diagonal sheets, so `VisusChop` and
  `VisusThrust` load them; the Flesh Maiden's projectiles take `PolearmArt.FleshMaiden.SheetPath`.
  `VisusChop`'s shadows go through `Place`, as the Flesh Maiden's smash already did.
  `VisusGrip.Axis` and `FrameHeight` (the level sheet's) are gone.
- **The geometry did not change.** Measured on the art: the shaft of both diagonal sheets runs
  through the butt the code has ((5.3604, 86.6396) in 92 px frames; (4.9756, 121.0244) in 128)
  with its middle on 0 across, and 120 and 168 texels to the point; `VisusGrip.Point`, `Teeth`,
  `Eye` and `FleshMaidenGrip.Point`, `Eye`, `Edge`, `Hook` all land on their parts.

**Her wings' beat** (`.Beat.cs`, new; `QueenSlimeWingbeat.png`, new). The user's note: the flap
was too quick and flat, and a wing is not straight all the time (references: a dragon's and a
bat's wingbeats, the arm leading and the hand trailing).

- **The art.** `work/queen/art/wingbeat.py`, from the same rig as the wings (`wings.py`): 24
  frames of one beat, keyed (upper arm, forearm bend, hand bend, fan) at frames 0, 3, 5, 8, 10,
  12, 14, 16, 18, 20, 22 and Catmull-Rom between. On the way down the arm goes first and the tip
  is still up; at the bottom the tip curls down and in; on the way up the wrist rises first and
  the hand hangs; the feathers close on the way up and open on the way down. Frame 8 is the
  wings' spread (`QueenSlimeWings.png` frame 5) pixel for pixel, so the opening hands on to the
  beat without a jump (checked: identical once the roots are lined up).
- **The clock.** A beat takes 100 ticks hovering, 74 at speed 9, 64 climbing at 6 up. A curve
  (keys in `BeatPose`) maps time to frames: held at the top, fast through the push, slow at the
  bottom and on the way up. The push is at 0.3 of the beat: her gel pressed (`BeatGel`) and a
  kick, the air thrown down off both wings (`WindStreak`s), the ground's dust blown out under her
  if it is within 22 tiles (`Downwash`), `Item32` pitched down. Her body sinks between pushes
  and is thrown up by them (`BeatBob`, up to 6 px either way, drawn only). The underside is
  darker on the way up (`BeatLit`); her colours flare as the wing turns into the push
  (`BeatSheen`). While it sweeps fast, faint shadows of where it was 3 and 6 ticks before.
- **Glides.** Every 3 to 5 beats, not climbing and slower than 7: the push slows into the spread
  and holds it 30 to 48 ticks, her body drifting down, then pushes the harder (1.3).
- **Falling** (vanilla's slam): the wings go up at three times the pace and stay up.
- The dash's tell holds the top frame; the charge keeps the old swept frame (`QueenSlimeWings`
  frame 2); her death's spasm beats through the new frames, fast and out of time. Frames 6 to 11
  of `QueenSlimeWings.png` are no longer drawn.
- Everything here is drawn by each client on its own clock (nothing sent), except the takeoff's,
  which runs off the scene's synced ticks, its second push included.

**The transition, stronger** ("the pierce and spread need more strength"):

| Step | Ticks | What |
|---|---|---|
| SPROUT | 22 | The folded wings stab out of her in 5 ticks, from 34 px inside her and a fifth of their size to 1.4 of it, flicked outward, then ring back (a damped spring, two shudders). A hard kick to her gel and a big ripple; 14 globs a side thrown high, sparks, a burst of light at each root, three streaks of air shooting up each side; a wet tear, the wing sound, a cast, a deep thump (`Item14` low); the screen jolted (8) and a flash of her colour; the glow behind her flares to 0.75 |
| SPREAD | 40 | They snap open in 12 ticks (frames 0 to 5, easing out) growing to 1.42, then ring down to 1.25. At the snap: her gel squashed, the ripple up; 28 puffs of dust a side blown along the ground, 7 streaks of air a side and 14 out all round her, light and sparks off the middle of each wing, the ground's own dust thrown up and the ground rolling out (`GroundWave.Slam`, 16 tiles, 6 px); Betsy's gust, the wing sound, a thump and a whoosh; the screen shaken (10) and a flash (0.22); the glow behind her to 0.85 |
| TAKEOFF | 90 | The wings lift from the spread to the top of the beat (14) while she sinks into herself, come down harder and harder (8), and at 22 the push (as before: 12 up, the shake, the ground heaved, the dust rising with her); the rest of that beat in 34; the second beat at the climbing pace, its push at 76: 5 more up and the gust. Then vanilla, flying, the beat going on from where the scene left it |

**Verified for this.** In the IL of tML 1.4.4 and its FNA: `SpriteBatch.Draw` takes the origin in
the destination quad and a flip only swaps texture coordinates (`GenerateVertexInfo`), so a
mirrored frame's pivot is `(x, height - y)`; `SoundStyle.PitchVariance` has a setter; FNA `Color`
has a `(int, int, int, int)` constructor. In the mod's own code: `GroundWave` only draws;
`WindStreak` is drawn along its velocity. Read only in the 1.4.5 decompile, not checked in 1.4.4:
vanilla's phase two flies with `noGravity` except in the slam's drop (`AI_121`, case 4); the beat
does not depend on it, it follows `noGravity` whatever sets it.

**Hit this time:** `BurstTicks` was already the arrival's (`.Rite.cs`), so the stab is `StabTicks`.
A glide's hold sat just after the push point, so the push fired before the glide: a gliding beat
pushes as it leaves the spread instead.

**Preview:** `work/queen/preview/transition2.py` (the crouch to the flight, with the stab, the
snap, the takeoff, beats and a glide) and `beat2.py` (the clock and curves, kept in step with
`.Beat.cs` by hand); `flight2.py` puts the old flap beside the new.

**Not yet confirmed in game:** the new load (the spears), how the beat reads at the game's own
scale and light, the downwash on uneven ground, the takeoff's second push in multiplayer.


---

## 16.14 Later in session 7: her slimes, her core, her numbers, her half, her plate, the frenzy, the arrival cleared

The user's notes, all on the Queen Slime: her slimes should "die by itself and give nothing but
effects after 10 seconds"; the core took 600, then 794, from a player and should take 150; her
Legends damage should be 1.1; she "sometimes revert[s] back to her wingless phase with wingless
attacks but wings as cosmetics"; her plate showed before the roar; the death's jumping went on
too long (the user chose eight seconds); the core should fly at half the speed; and "on her spawn
animation, every hostile npc below 500 hp is killed instantly".

| What | Where | How |
|---|---|---|
| Her slimes pop at ten seconds (every world) | `Content/NPCs/Legends/QueenSlimeMinions.cs` (new, a `GlobalNPC` on 658, 659, 660) | The server counts `age`; at 600, `Expire`: `popped` set, a `SyncNPC` sent (so a client has the flag before the kill reaches it), then `PADGlobalNPC.KillForNothing`. The last 90 ticks tell it: its colour lerped to white by a shine that beats faster and faster (0.15 to 0.9 rad a tick, the phase summed so it never jumps), its light, crystal dust off it. The pop (`HitEffect`, where `popped` and dead): `Item27` up and quiet, crystal dust, six globs, sparks, a small ball of light, over vanilla's own death. `SendExtraAI`: the bit and the age (a client's age moved on 6 ticks' drift). Her death's BURST pops them all the same way (`KillHerSlimes`). |
| Nothing given | `PADGlobalNPC.GivesNothing`, `PreKill`, `KillForNothing` | Set on the server just before `StrikeInstantKill`. `PreKill` false turns away her slimes' (and the cleared NPCs') items, `OnKill`, `DoDeathEvents`, money and hearts. Not synced: nothing drops on a client. |
| The core: exactly 150 | `QueenSlimeCore` | `Damage` 75 (what the game doubles to 150); `ModifyHitPlayer` adds `Exactly` to `modifiers.ModifyHurtInfo`, which sets `info.Damage` to 150 after defense and everything else. Why it took 794: 200, doubled as every hostile projectile's damage is against a player (`Projectile.Damage`: `Main.DamageVar(damage, -Main.player[owner].luck) * 2`), then doubled again by the world's difficulty (Expert ×2, Master ×3), which tModLoader applies to every hostile projectile that hits a player (`CombinedHooks.ModifyHitByProjectile`, below), less a little defense. |
| The core: half the speed | `QueenSlimeCore.PeakSpeed` 13 (was 26) | A way takes πR/13 ticks, 140 to 300 (was 70 to 150). Its shape and reach are the same. |
| Legends numbers | `LegendsQueenSlime.QueenDamage` and `QueenShots` 1.1 (were 1.2) | Contact damage, gel and shockwave. Life and defense stay 1.2. The grab's drain (50 a second) was never scaled and is not. |
| Her half | `PreAI` and `.Moves.cs` `Heal` | The cause: vanilla's `AI_121` works out her half from her life every tick (`life <= lifeMax / 2`), so the dash's 150 a second could lift her over half, and vanilla went back to its first half (hops, no flying) while her wings were still drawn. Now, once `transitioned` and not `dying`, life over half is cut to half every tick, and `Heal` never fills past half. |
| The plate waits for the roar | `BossPlate.BarWaitsForName` (new registration), `BossPlateEntry.BarWithName`, `BossPlate.FadeUp` | The cause: the name waited for her roar, but the bar faded in the moment vanilla reported her, at her spawn, all through the rite. Now, for a type registered so, the bar's fade is held at 0 while its name is still owed (`nameFlightPending`), and it comes up with the name when `Announce` starts it at ROAR. A player who joins after the rite gets both the moment she can be hurt, as before. Registered in `LegendsQueenSlimeSystem.PostSetupContent`. |
| Frenzy, eight seconds | `.Death.cs` `FrenzyTicks` 480 (was 900) | |
| The arrival cleared | `.Rite.cs` `ClearScene`, `Cleared`, `ClearBelow` 500 | Every tick of the rite, the server's: every NPC within `SceneRange` (3000 px) of where she forms, with less than 500 life, that is not friendly, not a town NPC, not a critter (`CountsAsACritter`, `Sets.CountsAsCritter`, `Sets.TownCritter`, `Sets.ActsLikeTownNPC`), not a boss or a part of a boss's fight (`boss`, `Sets.ShouldBeCountedAsBoss`, and `BossArrivalWait.OfAFight`: its `Parts`, `Heads` and `WormParts`), not `immortal`, not `dontTakeDamage`, and not a segment living on another's life (`realLife` elsewhere; its head is taken and the body goes with it), is killed by `KillForNothing`. The user did not say whether they should drop anything; they give nothing, so a summoning cannot farm them. To let them drop, call `StrikeInstantKill` instead. |

*[Superseded later in session 7: her slimes last thirty seconds (`Lifetime` 1800), not ten (600). See §16.15.]*

**Verified for this** (tML 1.4.4 IL):

- `NPC.NPCLoot`: `CountKillForAchievements`; the bestiary kill and banners only if there were
  interactions (`GetWereThereAnyInteractions`); the statue check (`AnyInteractions`, statue
  spawns only); then `NPCLoader.PreKill`, and only if it is true `DoDeathEvents_BeforeLoot`,
  `NPCLoot_DropItems`, `NPCLoader.OnKill`, `DoDeathEvents`, `NPCLoot_DropMoney`,
  `NPCLoot_DropHeals`. It returns at once on a multiplayer client.
- `NPC.StrikeInstantKill`: `StrikeNPC(new HitInfo { InstantKill = true }, false, true)`, then
  `NetMessage.SendStrikeNPC` in multiplayer. `StrikeNPC` does not look at `dontTakeDamage`;
  `immortal` keeps the life; an instant kill shows no number; `HitEffect` runs before
  `checkDead`, with the life already taken.
- `NetMessage.SendData` writes `NPCLoader.WriteExtraAI` for every NPC, vanilla types included.
  `MessageID.SyncNPC` is 23; `SendData` takes `number` by name.
- `NPC.CountsAsACritter` is `lifeMax <= 5 && damage == 0`, and not 594 or 686.
  `NPCID.Sets.CountsAsCritter`, `TownCritter`, `ActsLikeTownNPC`, `ShouldBeCountedAsBoss` are
  `bool[]`.
- `SetDefaults`: the bound town NPCs (105, 106, 123, 354, 376, 579, 589) and the Eternia Crystal
  (548) are `friendly`; the Target Dummy (488) is `immortal`. Her slimes: 658 and 659 are 24×18,
  `aiStyle` 1, 40 damage, 150 life; 660 is 40×30, `aiStyle` 14, 50 damage, 120 life; all
  `NPCDeath1`, no `value`.
- `GlobalNPC.DrawEffects(NPC, ref Color)` is called in `Main.DrawNPCDirect` before `PreDraw`;
  her slimes are drawn by the ordinary path (only 657 has its own in `DrawNPCDirect_Inner`).
- `HurtModifiers.ToHurtInfo` invokes `ModifyHurtInfo` last, after `SourceDamage` and
  `GetDamage`; `Player.Hurt` takes `info.Damage` from life as it is. A projectile's
  `ModifyHitPlayer` runs inside `PlayerLoader.ModifyHurt` (`CombinedHooks.ModifyHitByProjectile`),
  before any `ModPlayer.ModifyHurt`. Vanilla's endurance, solar, beetle and paladin cuts are
  `FinalDamage` multipliers, so they come before it too.
- **Every hostile projectile that hits a player is scaled by the world's difficulty, by
  tModLoader.** `CombinedHooks.ModifyHitByProjectile` calls the projectile's `ModifyHitPlayer`
  and the player's `ModifyHitByProjectile`, applies the banner's defense and the cold cut, then,
  unless the projectile was reflected or `ProjectileID.Sets.PlayerHurtDamageIgnoresDifficultyScaling`
  is set for its type, multiplies `SourceDamage` by `Main.GameModeInfo.EnemyDamageMultiplier`
  (Journey: the difficulty slider's strength). That is on top of vanilla's doubling in
  `Projectile.Damage`. So a hostile projectile of damage D hits for about 2D in Classic, 4D in
  Expert and 6D in Master, less defense. This is true of every boss's projectiles in this mod,
  as the line in §16.7 ("the world's difficulty multiplies it") says.
- `SoundStyle.MaxInstances` has a setter.

**Not yet confirmed in game:** the pop and its tell at the game's own scale; the clearing in a
busy spot (and in multiplayer, where every death is a strike sent); the bar and the name coming up
together at the roar; that her half holds through a whole dash.


---

## 16.15 Later in session 7: her slimes at thirty seconds, and the Fly Swatter

**Her slimes** last thirty seconds now, not ten (the user's word): `QueenSlimeMinions.Lifetime`
1800. Nothing else moves: the tell is still the last 90 ticks, the age still fits the short it is
sent in, and her death still pops them all at once.

**The Fly Swatter** (`Content/Items/Melee/FlySwatter.cs`, `FlySwatter.png`, new). The user's
spec: 3 iron and 2 web at an anvil; short; 20 damage, 50 against the Queen Bee, and any bee
(not her) killed at once.

| What | How |
|---|---|
| The item | Melee, 20 damage, knockback 3, swung (`ItemUseStyleID.Swing`), 18 ticks, auto swing, `Item1`; white, 3 silver. |
| Bees | `ModifyHitNPC`: `NPCID.Bee` (210) or `BeeSmall` (211), `modifiers.SetInstantKill()`. Hornets are not bees and take the swatter's plain damage. |
| The Queen Bee | `ModifyHitNPC`: `SourceDamage *= 2.5`, so 50 at base, and prefixes and melee bonuses scale it as they do the 20. Her defense still counts. |
| Short | A 32x32 sprite (16x16 art at 2x). A swung item's hitbox is the held sprite's frame times its scale (`Player.ItemCheck_GetMeleeHitbox`; 32x32 on a dedicated server), so the size of the art is its reach. |
| Recipe | `RecipeGroupID.IronBar` 3 (iron or lead), `ItemID.Cobweb` 2, `TileID.Anvils`. |
| Text | *Kills bees in one hit* / *Deals 2.5 times the damage to the Queen Bee* / *'Meant for flies. Nobody told the bees.'* |

*[Superseded later in session 7: 12 damage, 70 at base against the Queen Bee, hornets die in one swat too, and a swing goes through everything it touches. See §16.16.]*

**Verified for this** (tML 1.4.4 IL): a swung item's hit goes `Player.ProcessHitAgainstNPC` to
`CombinedHooks.ModifyPlayerHitNPCWithItem`, which calls `ItemLoader.ModifyHitNPC` first;
`NPC.HitModifiers.SetInstantKill` sets `_instantKill`, which `ToHitInfo` copies to
`HitInfo.InstantKill`, and the strike packet (28) carries it as a flag, so every machine kills the
bee. `NPCID` has no other bee (the hornets are 42, 176, 231 to 235 and the negative sizes; the
Cochineal, Cyan and Lac Beetles are beetles).

**Not yet confirmed in game:** the swatter's look in the hand and its reach against bees in the
air.


---

## 16.16 Later in session 7: the Fly Swatter again, and Rage! Sweet Fire

**The Fly Swatter**, the user's second pass: it should go through a swarm so all of it dies,
hornets should die too ("even those 300 sth hp hornets"), 12 damage, and 70 against the Queen Bee.

| What | How |
|---|---|
| Numbers | `Damage` 12. The Queen Bee: `SourceDamage *= 70 / 12`, so 70 at base, and prefixes and melee bonuses scale it as they do the 12. |
| Hornets | `Swattable`: `Bee`, `BeeSmall`, and the hornets `Hornet` (42), `MossHornet` (176), `HornetFatty`, `HornetHoney`, `HornetLeafy`, `HornetSpikey`, `HornetStingy` (231 to 235). Their sizes (the negative net IDs) share these types. The Moss Hornet has 220 life, the "300 something" of an Expert world. The Vortex pillar's Alien Hornet and Alien Queen are not included. |
| Through | Vanilla's swing hits one thing, then waits a third of the swing: `Player.ItemCheck_MeleeHitNPCs` skips every NPC while `attackCD > 0`, and `ProcessHitAgainstNPC` ends with `ApplyAttackCooldown()` (`attackCD = max(1, itemAnimationMax * 0.33)`). A hook on `On_Player.ItemCheck_MeleeHitNPCs` (TerrariaHooks), for the swatter only, runs the loop again with `attackCD` cleared for as long as something new is hit, and leaves it at 0. Each NPC is still hit once a swing (`meleeNPCHitCooldown`). Hooked there and not on `ApplyAttackCooldown`, which is small enough that the JIT could inline it into its callers, where a detour would not reach. |
| Text | *Kills bees and hornets in one hit* / *Hits everything its swing touches* / *Deals 70 base damage to the Queen Bee* / the flavour line as before. |

**Rage! Sweet Fire** (`Content/Items/Mage/Incantation/RageSweetFire.cs`, new): an incantation,
Flame, Enchantment. Asked for as: *25% damage increase for every fire related items/projectiles
(flamethrowers, solar stuffs and anything that has the affinity for burn)*; a moving fire aura
round the player, light; a Lava Bucket, 15 Souls of Fright, 50 Living Fire Blocks and a Book at
the altar. The user's answers: the element is **Flame** (the mod's own, as Meagre Flame's), the
burns are On Fire!, Hellfire and Daybreak only (*Cursed Inferno and Shadowflame are dark*, and
Frostburn was not chosen), and the aura only shows.

| What | Where | How |
|---|---|---|
| The cast | `RageSweetFire` | `Castable`, incantation, 0 damage, 40 mana, use time 30, Pink, 4 gold. Puts on Sweet Fire for 30 s; not again while more than 5 s are left (`CanCast`), as the other two. Written at the Makeshift Altar. |
| The buff | `Content/Buffs/SweetFire.cs` (`SweetFire`, `SweetFirePlayer`) | `SweetFirePlayer.ModifyHitNPCWithProj` and `ModifyHitNPCWithItem`: a fire hit's `SourceDamage += 0.25`, where the hit is worked out (the owner's machine). |
| What is fire | `Content/Combat/FireAffinity.cs` (`FireAffinity`, `FireAffinitySystem`) | Projectiles by type: `FireArrow`, `BallofFire`, `Flamarang`, `Flamelash`, `Sunfury`, `Flames` (both flamethrowers; in 1.4.4 the Elf Melter shoots it with `ai[0]` 1, the cold flame, and it counts), the five flares, `InfernoFriendlyBolt`/`Blast`, `ImpFireball`, the Molotov and its three fires, `Spark`, `WandOfSparkingSpark`, `Cascade`, `HelFire`, the three Flameburst shots, `DD2PhoenixBowShot`, `FireWhip`, `FlamingMace`, `Volcano`; the sun's `SolarWhipSword` and its explosion, `Daybreak` and its explosion, `SolarFlareChainsaw`, `SolarFlareDrill`, `SolarCounter`; `HellfireArrow`; and `MeagreFlameBall` (added in `PostSetupContent`). Swings by item: `FieryGreatsword`, `MoltenPickaxe`, `MoltenHamaxe`, `DD2SquireDemonSword`, `WaffleIron`, `SolarFlareAxe`, `SolarFlareHammer`, `SolarFlarePickaxe`, `LunarHamaxeSolar`. And melee set alight the way vanilla does it: a swing or a melee projectile with the Magma Stone, a swing, melee projectile or whip with the Flask of Fire (`meleeEnchant` 3), not with `noEnchantments`. Never `npcProj` or `trap`. |
| The ring | `Content/Particles/SweetFireAura.cs`, `SweetFireAura.png` (new) | One per player, on every client (made by `SweetFirePlayer.PostUpdate` while the buff is on). Three flames going round a flattened circle at the waist (30 by 10, 6 px under the centre), 0.07 rad a tick; a 12-dot tail each, 0.1 rad apart, cooling white, yellow, orange, red and rising 10 px; the far side dimmer and smaller; a little light at each flame; an ember now and then. Grows in over 20 ticks; the owner's fades over the buff's last second; when the buff goes it throws 16 embers out and goes over 16 ticks. Drawn about the player's position of the moment, so it keeps to them. The dot is 16x16, white, stored premultiplied, drawn in the alpha pass as light plus a little cover, so it keeps its colour on a bright sky; drawn additive, the first try, it went white there (preview). |
| The cast's look | `Content/Projectiles/Magic/SweetFireCall.cs` | As `NurtureCall`: hidden, sent to everyone. Embers drawn in for 12 ticks with a breath of fire (`Item20`), then at 20 a burst: two balls of light, sparks, torch dust, `Item20` and `Item14`. |
| Art | `RageSweetFire.png` (page, 60x60), `Content/Buffs/SweetFire.png` (32x32) | Placeholders, rows in `PlaceholderTextures.md`. `work/fire/art.py` draws both (the page on `IncantationTemplate.png`). |
| Text | `Items.RageSweetFire`, `Buffs.SweetFire`, `Projectiles.SweetFireCall` | *Asks the fire to rage with you a while* / *Your fire deals 25% more damage* / *Flamethrowers, solar weapons and anything that sets foes alight* / *Can only be asked again as it fades* / *'Whoever named it had not yet been burned.'* The buff: *25% increased damage with fire*. |

*[Added to later in session 7: other mods' fire is learned as it is used, and mods can say so through Mod.Call. See §16.17.]*

**Verified for this** (tML 1.4.4 IL):

- Where vanilla sets an NPC On Fire! (24), Hellfire (323) or Daybreak (189): `Projectile.StatusNPC`
  and `Player.StatusToNPC` only. `Projectile.Kill`'s and `VanillaAI`'s other `NPC.AddBuff` calls
  give Lovestruck and the like, Wet, Slimed and Poisoned. The projectile list above is every
  type-guarded fire block of `StatusNPC` but `FlamesTrap` (188, a trap's): a small parser over the
  IL (`work/fire/blocks.py`), every fire `AddBuff` in the method then matched to its guard, and
  the ones it could not follow read by hand (954's and 15's buff is a local, the whips are a
  `switch` on `type - 912`, of which 913 is the Firecracker). 979's local is Frostburn. The item list is `StatusToNPC`'s switch.
- `ModPlayer.ModifyHitNPCWithProj(Projectile, NPC, ref NPC.HitModifiers)` and
  `ModifyHitNPCWithItem(Item, NPC, ref NPC.HitModifiers)`. `Projectile.melee` is internal in
  1.4.4; it returns `CountsAsClass(DamageClass.Melee)`, which is what is used.
- `StatModifier` has `+` and `*` with a float. `Player.HasBuff<T>()`.
- The strike packet (28) carries `HitInfo.InstantKill` as a flag.
- A swing's hit goes `Player.ProcessHitAgainstNPC`, `CombinedHooks.ModifyPlayerHitNPCWithItem`,
  `ItemLoader.ModifyHitNPC` first.

**Previews:** `work/fire/aura_preview.py` (the ring and its embers on a dark and a bright ground,
the player standing, then running, then the buff ending).

**Not yet confirmed in game:** all of it: the swatter's pass through a swarm, the ring's look at
the game's own scale and light, the damage on a hit (the number should read 25% higher for a
fire weapon with the buff on), the recipe at the altar.


---

## 16.17 Later in session 7: Sweet Fire and other mods

The user asked whether flame minions, flame swords and other mods count. Vanilla's did already:
the Imp's fireballs, the Flameburst sentries' shots and the Firecracker are in the projectile
list; the Fiery Greatsword (and its volcano), Brand of the Inferno, Waffle's Iron, the molten
and solar tools are in the item list; and any swing or melee projectile the Magma Stone or the
Flask of Fire sets alight counts. Other mods' did not, and now do.

| What | Where | How |
|---|---|---|
| Learned | `FireAffinityPlayer` (in `FireAffinity.cs`) | `ModifyHitNPCWithProj` / `ModifyHitNPCWithItem`, if the projectile or item is another mod's and not yet known (and no Magma Stone or Flask of Fire could be the burn): note `FireAffinity.Burn(target)`, every fire debuff's ticks added up. `OnHitNPCWithProj` / `OnHitNPCWithItem`: if it went up, the type is fire from then on (added to `Projectiles` or `Items`, logged once). The first burning hit itself is not raised; the sets start over each session. Vanilla types are never learned: their lists are exact. |
| Fire debuffs | `FireAffinity.Debuffs`, `NamedFire`, `AddModded` | On Fire!, Hellfire, Daybreak; and at load every other mod's debuff (`Main.debuff`) whose internal name has a word that starts `fire`, `flame`, `burn`, `blaz`, `inferno`, `ember`, `scorch`, `ignit`, `magma`, `lava`, `pyro`, `incinerat` or ends `fire`, `flame`, `flames`, `burn`, `blaze`, and none that starts `frost`, `ice`, `cold`, `chill`, `freez`, `frozen`, `cryo`, `snow`, `glacial`, `curse`, `shadow`, `dark`, `void`. Words split at capitals, digits and underscores. Run on sample names with the compiled method: BrimstoneFlames, HolyFlames, Dragonfire, GodSlayerInferno, BanishingFire, Hellfire, Sunburn yes; Frostburn2, FrostFlames, IceBurn, CursedFlames, Shadowflame, DarkFlames, Nightwither, Remembered, Searing no. The ones found are listed in `client.log`. |
| Said | `PisAndDag.Call`, `FireAffinity.Call` | `Mod.Call("FireProjectile", type)`, `("FireItem", type)`, `("FireDebuff", type)`; true when taken. For a mod whose fire does not burn, or burns with a debuff the names miss. |

**Verified for this** (tML 1.4.4 IL): `CombinedHooks.ModifyHitNPCWithProj` calls
`ProjectileLoader.ModifyHitNPC`, `NPCLoader.ModifyHitByProjectile`, then
`PlayerLoader.ModifyHitNPCWithProj`; `OnHitNPCWithProj` calls `ProjectileLoader.OnHitNPC`,
`NPCLoader.OnHitByProjectile`, then `PlayerLoader.OnHitNPCWithProj`. The item hooks are in the
same order (`ItemLoader`, `NPCLoader`, `PlayerLoader`). So a player's hooks see the hit last, both
before and after it, after the projectile's or item's own. `BuffLoader.BuffCount`,
`BuffLoader.GetBuff`, `Main.debuff`, `NPC.maxBuffs`, `Lang.GetProjectileName`,
`Lang.GetItemNameValue`, `Mod.Call(params object[])`.

**Not yet confirmed in game:** learning against a real mod's weapon (Calamity's, say), and the
names the heuristic picks in a big modpack (read `client.log` for the list).



---

## 16.18 Later in session 7: Dona Quinn and Triunfo

A boss asked for in one long message (kept in the chat): a girl knight on a donkey who stops the
player on the Cleared Ground, charges with a lance, is thrown at one life, begs, and is spared or
finished. Her and the donkey are two NPCs, by request. The user's answers to the questions asked:
a hit on the donkey is a hit on her (shared life); after the fall the donkey bolts and comes back
near her; the lance is a flat 30 in every world, varied as vanilla varies any hit; the user's typo
line reads *"...kind of knightsome and admirable overall."* Mid-build the user added: the donkey
turns with a plain flip (no squeeze); the lying head exactly as `DonnLyingdownRef.png`, raised at
most 15 degrees to beg; the player walked very close (3 to 5 blocks), turned to face her, and
the two facing each other; a fade to black out of play into the scene (a new `Cinematic.Fade`);
and the hair, a layer of its own for this, sways.

| What | Where | How |
|---|---|---|
| The rig | `Content/NPCs/DonaQuinn/DonaRig.cs` (new) | Every number measured off the art. Triunfo: 126x1640, 20 frames of 126x82 (0 stand, 1 to 7 run, 9 to 14 gallop, 15 air, 16 to 19 reared). The saddle found in every frame: seat (58, 40), up 2 on 3, 4, 7, 8, 12, 13, up 6 on 15, and on the rear frames the same saddle turned 27 degrees nose up and moved (-1, -6). Her canvas 42x60; riding seat (21, 46), shoulder (17, 34), fist (30, 41), nape (20, 32); standing middle (23, 43), shoulder (18, 30), nape (20, 30). Lance 22x76, grip (11, 65). `Pose` holds every piece's place and turn; `Piece` draws a piece mirrored about its own pivot (XNA's origin is in the flipped picture's pixels); `Band` draws rows of a piece, for the hair. |
| Lying | `DonaRig.LieNape`, `LieShoulder`, `LieHeight` | Fitted to `DonnLyingdownRef.png` by trying every turn and place of each part: body a quarter over, middle (17, 23.5); head upright, nape (33, 27); arm 64 degrees out from hanging, shoulder (26.5, 24); the parts rebuild the picture to within 19 of 852 pixels. On the body's canvas the lying nape is (26.5, 27) and shoulder (23.5, 33.5); `OnFoot` slides from the standing ones by sin squared of the turn. Her middle lies 10.5 over the ground (the hand under her chin touches it); part way up, never lower than her boot's corner reaches (`BodyHalfWidth` 9, `BodyHalfHeight` 17). |
| Triunfo | `TriunfoTheDonkey.cs` (new) | 64x48 body, `realLife` = her while she rides (kept every tick on every machine), so the one bar counts both; never dies (`CheckDead` false). 37 mph top (7.24 px/tick: the stopwatch's 216000 / 42240 the other way), reached in 1.5 s; `Collision.StepUp` for single steps, a 7.5 hop when blocked. Run and gallop step by ground covered (7 px, 26 px a picture), so the legs never skate. Turning is a flip. Modes: Carry (her `Reins`), Bolt (150 ticks away), Wait, Return (a walk back, 56 px off her), Stand; gone when unwatched and alone. Drawn by her while she is in the saddle (`InSaddle`), so the two never come apart. |
| Her | `DonaQuinn.cs`, `.Fight.cs`, `.Fall.cs`, `.Scene.cs`, `.Draw.cs` (new) | 1500 life flat in every world (`ApplyDifficultyAndPlayerScaling`), defence 10, no touch damage, `Music` Boss1, boss head from her own face (`DonaQuinn_Head_Boss.png`). Phases: Intro, Challenge, Charge, Brake, Rear, Turn, Ready, Buck, Thrown, Lying, Plead, GetUp, Talk, Free, Finish, Leave. `NPC.boss` only while she fights, so the bar goes at the fall. A blow that would kill her leaves her on 1 (`CheckDead`) and Triunfo bucks her; only the finishing cut lets her die (then `SyncNPC` by hand: `checkDead` sends nothing). |
| The lance | `.Fight.cs` `LanceStrike` | Each machine for its own player, couched (within 0.3 rad of a quarter turn and a touch down) and at 2 px/tick or more; the shaft from 14 px past the fist to the point, 10 thick (`Collision.CheckAABBvLineCollision`). `Main.DamageVar(30, -luck)`, `Player.Hurt` with knockback 7.5 and the death line *"{0} was run through by a knight who was never knighted."* |
| One blow | `OnHitByItem`, `OnHitByProjectile`, `ShareSwing`, `ShareShot` | A swing or a shot that lands on one of the two makes the other immune to it as if it had been hit too, the way `Projectile.Damage` does (by id, by projectile, or the owner's 10 ticks for a shot that goes through); otherwise a piercing shot through both took her life twice. |
| The fall | `.Fall.cs` | A body thrown: (3, -5.4) plus 0.3 of his speed, pitched to come down 0.35 past face down (the arc run forward to find the landing), a bounce at a third, a skid, the turn settling flat on a spring; head and arm on loose springs. The landing is read the way vanilla leaves it: `collideY` and `oldVelocity`, never `velocity.Y == 0`. Blood now and then, a little pool while she lies, a shake now and then. Getting up, the arm and head are eased against the world, not her body. A machine that only hears of the throw late (joined late) finds her down already (`Down`). |
| The lance after | `DonaLanceGore.cs` (new) | A `ModGore` with its own flight (`Update` returns false): thrown up and back the way she came, end over end, bounces off the floor at a third, lies flat, fades. |
| The scenes | `.Scene.cs` | On the stopped player's machine. Road: *"Fear me, you simpleton!"* with her face black and "???" on the tab; *"For it is I, Dona Quinn! Challenge me if you dare!"* (her name in it names her: the face comes up, the tab says *Dona Quinn, the Horseback Fool*), the camera pans to her; Decline / Accept, both *"Coward! Here comes my lance!"*; her name rises (`BossIntro.PlayNameOnly`). If nobody answers in 60 s she says it anyway. The player faces her through every scene (`DonaScenePlayer.FaceToward`). |
| The walk up | `DonaScenePlayer` (in `DonaQuinnEncounter.cs`) | A beat on her (40 ticks), fade to black (24), black (14: at its third tick the player is put on her head's side 11 blocks off, on their feet, facing her, off any mount, hook or rope, and the shot snaps in close: lean 1.4, bars, camera between them), fade in (30), then a slow walk (vanilla's run held to 1.6 px/tick in `PostUpdateRunSpeeds`) to 4 blocks off her middle. Then the server is told (`Choice.Arrived`) and she begs; 12 s is the server's limit if the word never comes. |
| Plead, spare, finish | `.Scene.cs`, `.Fall.cs` | Her head up 15 degrees; *"Please! I surrender! White flag! Me rindo!"*; Spare and Finish in the box's two corners (`Dialogue.OfferCorners`). Spare: up, still bleeding and shaking, turns to them, talks (the user's six lines, sir/madam and Mr/Mrs by `Player.Male`, the answers *...*, *...*, *Why did you do this*, *Why the knight stuff?*, *I am in a hurry*), then stands about looking at them or the ground and turning round now and then, until nobody sees her, and is gone. Finish: a `SlashStreak` cut (reach 30, width 14), blood both ways, her head drops, she fades and dies, and drops the Locket of a Book-Crazed Girl. Either way, never again in that world. A box that goes down mid-plead or mid-talk is put back; another speaker cannot be opened over a scene. |
| Her voice | `Content/UI/Bark.cs` (new) | Barks: a line over a speaker's head, warm cream with a faint breathing gold halo and a dark rim, rising in, holding by its length, fading out. Hers, now and then: *The grrreat Dona Quinn shall be victorious!* / *Take that you simpleton!* / *How do you like that? Also your hair is atrocious!* |
| The hair | `.Draw.cs` `DrawHair`, `HairShift` | Drawn in bands of two rows, top still, each lower band pushed sideways in whole pixels: a slow wave always (1 px), the wind out of doors (`Main.WindForVisuals`, up to 1.8 its way and a flutter), and the ends trailing when she moves (up to 2). |
| The encounter | `DonaQuinnEncounter.cs` (new) | Grand world, before hardmode, after the Eye's finale (`PugreamPresence.Stage` 1 or more), a player on the Cleared Ground (30 tiles in from its ends), on their feet, at the surface, dry, no boss up. Night or rain: the world is run on to 8 AM (`ModifyTimeRate`, about 150 ticks however far) and the rain stopped, the player held with the bars in. She comes 1150 px off on the Mile's side (950, 1350, 800, either side, if there is no ground). Stage saved and sent: 0 never met, 1 met, 2 spared, 3 finished. Packet `PADPacket.DonaQuinn` carries the player's answers. Nothing spawns round them from the road to "Coward!" and from the fall on (`DonaQuinnQuiet`). |
| Dialogue | `Dialogue.cs`, `DialoguePortrait.cs`, `TalkOptions.cs` | The panel's name tab: "???" until named, then name and title (`DialoguePortrait.Name`); `KnownAs` for a word in place of "???" (Mors: *The Tall Elder*). Faces of several layers (`Register(type, string[], frame)`). `Converse` (a speaker starts the talk, locked), `Line`, `Offer`, `OfferCorners`, `LineDone`, `Speaking`. |
| The fade | `Cinematic.Fade(amount)` | Asked every tick with how black, 0 to 1; the asker does the timing. Drawn over the cover, under the shade, roar, bars and interface. |
| The locket | `Content/Items/LoreItem/LocketOfABookCrazedGirl.cs`, `.png` (new) | Accessory, 1 defence, lore item. *A rusted gold locket, with paper folded small inside.* / *"Dear lovely daughter, I know you will be a knight one day."* / *Read until the folds went soft. It was never signed.* Flavour: *'It was opened more often than it was closed.'* Sprite a placeholder. *[The user has since rewritten the three verses by hand. As they are: §16.23.]* |

**Verified for this** (tML 1.4.4 IL):

- `NPC.UpdateCollision`: `oldVelocity = velocity`, `collideX`/`collideY` cleared, then set by
  `Collision_MoveWhileDry` where the collided velocity differs. `Collision.TileCollision` leaves
  `velocity.Y` as the gap closed on a landing, not 0. An NPC with `noTileCollide` skips all of it
  (`position += velocity`), so its `collideX`/`collideY` keep whatever they last were.
- `NPC.checkDead` sends nothing (`NPCLoader.CheckDead`, the sound, `NPCLoot`, `active = false`);
  vanilla's despawn tells clients with `life = 0` and `SendData(23)` (`NPC.CheckActive`).
- `Projectile.Damage`'s immunity (above); `perIDStaticNPCImmunity` is `uint[][]`,
  `localNPCImmunity` `int[]`, `appliesImmunityTimeOnSingleHits` exists; then
  `CombinedHooks.OnHitNPCWithProj`.
- `Collision.StepUp` checks no velocity. `PlayerLoader.PostUpdateRunSpeeds` runs right before
  `Player.HorizontalMovement`. `Main.WindForVisuals` (= `windSpeedCurrent`, about -0.85 to
  0.85). `MessageID.PlayerControls`, `Player.RemoveAllGrapplingHooks`, `Mount.Dismount`,
  `Player.fallStart2`, `Player.pulley`.
- `ModNPC.BossLoot(ref int)` (the `(ref string, ref int)` one is obsolete, CS0672).

**Errors hit and fixed:** the landing read as `velocity.Y == 0` after a fall measured the gap,
not the fall, and could miss the first landing (cry, blood, pool) altogether; the finish's death
never reached clients; a piercing shot through both took her life twice; nobody answering on the
road left Decline / Accept up through the charge; a client could see her half stand and drop
before getting up; for one frame on the throw she stood on his back (`InSaddle`).

**Previews:** `work/dona/sim5.py` + `sim_run5.py` (the fight, the throw, the fall, lying, the
plead, getting up, with the C#'s numbers), `work/dona/fitref3.py` (the lying fit).

**Not yet confirmed in game:** all of it. The encounter's trigger on a real Grand world, the time
skip, the charge's reach and the lance's hit, the fall on uneven ground, the fade and the walk
up, the hair at the game's scale, multiplayer.

**Open:** her title, *the Horseback Fool*, was mine (the user asked for a title, not which); the
locket's sprite and the boss head are stand-ins.

**Testing** (added after delivery): `/dona` says where she stands in the world; `/dona summon`
starts her road for whoever typed it, anywhere, with none of the trigger's checks (night or rain
still runs on to the morning first); `/dona reset` ends any road, removes her and Triunfo, and
puts her back to never met with her name unheard. `DonaQuinnCommand.cs` (a `CommandType.World`
command: the server runs it in multiplayer), calling `DonaQuinnEncounter.Summon`, `Reset` and
`Describe`. Listed for removal in `RELEASE_TODO.md`. Her name heard is each machine's own in
multiplayer, so a reset there forgets it only on the server.

**Fixed after the user's first test:**

- *She died without her fall, sometimes, when killed fast.* The killing blow had landed on
  Triunfo. `NPC.StrikeNPC` on an NPC with `realLife` takes the damage off the real one, then
  copies the real one's life into the struck one's own (IL 0x04d9), then calls the real one's
  `checkDead` (0x086b), which put hers back to one; his stayed at nothing, and
  `NPC.UpdateNPC_Inner` deactivates anything on nothing at its next update, before AI, without a
  word (0x036f). He vanished, and she with him (her AI ends her when he is gone from under her).
  Now his `HitEffect`, which `StrikeNPC` calls after the copy, keeps him on one or more; and if
  he is ever gone from under her as she is bucked, she is thrown anyway. Damage over time does
  not do this: `UpdateNPC_BuffApplyDOTs` takes it off the real one only, and strikes the real one.
- *She turned her head round over her shoulder with her body facing away* (the Mors way). Not
  wanted for her: her head now always faces the way she does, and to look at someone behind
  her the whole of her turns round (`Turn`): in the talk, toward the one who spared her; left
  alone, toward whoever is nearest while she looks at them, and of her own accord now and then
  while she looks at the ground. Her mood (`lookingDown`) is the server's and is sent with her.

## 16.19 Later in session 7: 'Dame' Quilt's moves, Triunfo's sense of the ground, the Queen Slime's death shot

**Her name in game is now 'Dame' Quilt** (the quotes are hers): the hjson `DisplayName`
(`"'Dame' Quilt"`, double-quoted so Hjson keeps the single quotes), `BossName`, the dialogue tab,
`/dona`'s replies. In her own lines she says it straight: "For it is I, Dame Quilt! ..." and
"The grrreat Dame Quilt shall be victorious!". The tab still waits for her to say it: new
`DialoguePortrait.SpokenAs(type, "Dame Quilt")`, which `Hear` matches instead of `TypeName`. The
files, classes and the saved stage key keep the name Dona Quinn.

**Her moves** (`DonaQuinn.Fight.cs`, all damage off the lance through armour: `Player.Hurt` with
`scalingArmorPenetration` 1; all varied by `Main.DamageVar`; none grown by the world):

| Move | What it is |
|---|---|
| Charge | Lance aimed at the player's middle as she comes (clamped from 0.1 over level to 0.45 under), struck from 10 px past her fist to 10 past the point, 24 thick (was 14 to the point, 10 thick, couched over a standing player's head). 30 at 2 px/tick, 67 flat out (7.24), by Triunfo's speed. Rides on until 300 px past where the player is now, not where they were. |
| Throw | Phase `Wind`: stops, lance drawn back, let go at tick 32 of 56 on an arc (`DonaLanceThrown.Aim`, gravity 0.35, flight 24 to 64 ticks by distance, half the player's run allowed for). 52 off its head in the air, once. It goes into the ground point first, 12 px, and stands there (drawn behind tiles). Only from 160 to 640 px, and 360 up or down. |
| Ram | Straight after the throw, lanceless: Triunfo runs them down, 15 to 40 by speed, ordinary damage, knockback 8, once. Careful: pulls up at walls. |
| Fetch | Back to the lance (`lanceX`, sent), slowing, and out of the ground as she passes within 24 px; then the rear, the lance waved. If she cannot get to it: back in her hand after 8 s of fetching, 15 s without it, or stuck. |
| Close, Swing | In close (4.6 px/tick, slowing to stop with them 16 to 70 px in front of her and not 70 over or under her fist), then the swing, 84 ticks, as the Visus chop is written (`Easing`): lowered to the ground in front, up hard (to -35 degrees), a hang, down harder, back. 40 through armour, once a swing, checked every 4 degrees of the sweep. Up: thrown (4.62, -13.86) spinning: 15 tiles up and 20 along on the player's 0.4 gravity. Down: (3.5, 5) rolling, flat. A second down once they stop (`RagdollPlayer.Throw`, lie 60). The CC tell (`CCBypass`: star, note, yellow outline of her, the lance and Triunfo through `ICCBypassOutline`) from the first tick of Close to the last tick the downswing can land. |
| Crash | A charge into something Triunfo cannot jump (`Crashed`: stopped dead over 3 px/tick, dry, and the column he met probes as a wall): `StrikeNPC` 100 flat on the server, sent with `SendStrikeNPC`; Triunfo knocked back off it and rearing, her head thrown back, dirt, "Ouchh!". Only a charge crashes. |
| What next | The server's (`NextAttack`): without the lance, Fetch. If Triunfo cannot get to them (`DonkeyNav.Reachable`, which also wants the ground he reaches within 3 tiles of their feet): the throw if in range, else ride to 320 px off (`Range`), else (too far above or below to throw) rear and wait. Otherwise nothing but a charge twice running: after a charge, 35% the swing (if they are level with the lance), 30% the throw. |

**Triunfo's sense of the ground** (`DonkeyNav.cs`, new, pure; the user's two screenshots: nose
against a three-tile bank doing nothing, and sunk on a lake bed). Every tick he is on the ground
and going somewhere he reads the columns from under his back to 56 out, walking his eye along
the ground a step at a time, into a row of tops (nothing to stand on is +infinity, a wall and
everything past it -infinity), and finds the first rise of two tiles or more, wall or pit within
16 columns. A rise he can jump (10 tiles) he jumps when the least jump that lands him on it or past
it (flown ahead over the row of tops in vanilla's order, gravity 0.3 after the AI, sideways into a
column whose top his feet are not 3 px over is into its side, gently he only stops) would be more
a tick later; with room to go on 3 ticks after he lands. Stood against one with no speed, the
server has him back off up to 6 tiles of level ground, still facing it, and come again (sent: the
run-up is in his `SendExtraAI`); without room, he leaps from where he stands. A pit he leaps if 14
tiles or less and the far side is in reach at the speed he will have, otherwise he pulls up on the
brink (0.25 px/tick of braking, 6 px short), as for lava and deep water he is not to swim. A wall
he pulls up for if careful; a charge is not careful. In deep water (3 tiles or more) he swims: feet
held 30 px under the surface, 3.2 px/tick (halved by the water), out with a leap at a bank. Gaps
narrower than he is long he walks across. A hop of a tile if something his eye did not make out
stops him. His legs go backward backing off, and paddle swimming.

Tested in a harness (`work/nav/Harness.cs`, `build.sh`) that runs `DonkeyNav.cs` itself
(`-define:PREVIEW`, tiles from a grid) against vanilla-like movement (`StepUp` of one tile,
X-then-Y tile collision, gravity and water as vanilla): steps of 2 to 10 charging and careful
(over, no contact), 11 and 14 (crash; careful stops), pillars, pressed against 3, 6, 9 (back off
and over), cornered with no room (straight up and over), gaps 3 to 12 (over), 18 to 40 (stops),
drops of 5 (off) and 14 (stops), stairs, terraces (over, with bumps), a lake charging (stops) and
swimming (across and out), sunk on a lake bed (up and out), off a cliff into a lake, a platform
bridge, a low roof, lava. Trajectories drawn in `work/nav/sheet1.png`, `sheet2.png`.

**Reviewed** (an independent pass over the diff, harness runs of its own): five real faults found
and fixed: a throw at a player behind a wall sent the ram into that wall and she crashed herself
to death in about 90 s (the ram is careful now and only a charge crashes); an unreachable player
she could not throw at had her loop Close and Ready with the CC tell going off twice a second
(`Range`, `Rear`, and Close only when they are level with the lance); a swing that killed the
player threw them on respawn (`RagdollPlayer` now ignores the dead and clears itself on `Kill`
and `OnRespawn`, which helps the Abomanation's whip too); the lance and the ram struck while
Triunfo backed off for a run-up (forward speed only, never while backing); the tell ended a tick
before the downswing's last hit.

**The Queen Slime's death shot** (`LegendsQueenSlime.Death.cs`; reported as the King's, it was
hers): her last three seconds asked only for the bars and a lean, so the lean zoomed on wherever
the camera was, the player. Now the camera goes over to her over the first 60 ticks of the
shudder (`PisPlayer.BlendCamera`), holds on her through the burst, and walks home over 60 ticks as
she goes (`PanHome`, once, for a player who was in the shot); the lean is on her because the
camera is.

**Verified for this** (tML 1.4.4 IL): `NPC.UpdateNPC_UpdateGravity` (0.3, max 10; in water 0.2,
max 7; honey 0.1 and 4; shimmer 0.15 and 5.5; less high up), set before AI, added after it;
`NPC.UpdateCollision` moves a wet NPC by `velocity * waterMovementSpeed` (0.5) after colliding the
whole velocity; `NPC.StrikeNPC(HitInfo)` takes `hit.Damage` as it is (no defence, no
`dontTakeDamage` check), then checkDead.

**Not yet confirmed in game:** all of it. In particular vanilla's own collision against the flown
arcs (slopes, half tiles), the swim leap out of water, the throw's arc at long range, and
multiplayer.

**Open:** the numbers that were mine: 10-tile jump, 14-tile leap, the fetch's give-up times,
the move odds (35% swing, 30% throw), and what she does at a player she can neither reach nor
throw at (rears and waits).

---

## 16.20 Later in session 7: the Guide's first words

The user's request: in a freshly created world, ten seconds after the player first walks, an
unescapable talk with the Guide, with the world still meanwhile (nothing spawns); the screen
fades to him walking up to the player; his lines (the user's, below); a new lore item, the Piece
of Paper; his question with four answers in the four pillars' colours; a kit for each answer;
out of the cinematic.

| Work | Where | Notes |
|---|---|---|
| The scene | `Content/NPCs/GuideIntro.cs`: `GuideIntro` (the world's side, a `ModSystem`), `GuideIntroPlayer` (the screen), `GuideIntroGuide` (him), `GuideIntroQuiet` (spawns, chat) | See below. |
| Piece of Paper | `Content/Items/LoreItem/PieceOfPaper.cs`, `.png` (placeholder, 24x28) | Lore item (LoreLock keeps it). |
| Coloured answers | `TalkOption.Colour` (`Content/UI/TalkOptions.cs`) | A colour in the gold's place, on the gold's pulse (`Main.mouseTextColor`), grey once heard like any other. Null is gold, so nothing else changed. |
| Packet | `PADPacket.GuideIntro` (appended, after `DonaQuinn`) | Client to server: `Dark`, `Done`, `Abort`. |
| Test command | `/guideintro [reset]`, `GuideIntroCommand.cs` | In `RELEASE_TODO.md`. |

**Which worlds.** `GuideIntro.Pending`, saved as the key `PADGuideIntro` (present means owed), set
in `PostWorldGen`: only a world generated from now on has it, and a world loses it when the scene
is through to the end. A scene broken off (the player left, died, or the Guide went) leaves it
owed, and it starts again from the count the next time somebody walks. Every world size.

**How it runs.** The server (or single player) decides, in `PostUpdateWorld`:

| Beat | What |
|---|---|
| `None` | Owed. The first player on their feet moving over 0.5 px/tick is it (`Target`). |
| `Counting` | 600 ticks. Stillness round them: the buff on their machine (and with it Dialogue's lean in), and `GuideIntroQuiet.EditSpawnRate` on the server. |
| `Ready` | Their machine waits for them to stand (on the ground, dry, no hook or rope: never forced, since held in water they would drown and taken off a hook they would fall), shuts any chat, then holds them (`Cinematic.Hold`, the bars, every control off, the inventory key too) and fades to black over 30 ticks. On the second tick of the black it takes them off any mount, hook or rope and tells the server (`Dark`). The server gives up on a `Ready` after 1200 ticks, and it is owed again from the next walk. |
| `Coming` | Under the black the server puts the Guide down (`Bring`): on the side he was on, 15 tiles off (then 13, 17, 11, 19, 9, 7, then the other side), where `DonaQuinnEncounter.Ground` finds him a place and `Walkable` finds a walk to the stop (each column a step up of one tile or a drop of up to four, room for him, no liquid); failing a walk, anywhere at those distances with ground; failing that, beside them; failing that, where they stand. A world with no Guide gets one (`NPC.NewNPC`, which names him). His `ai[]` zeroed (he stands; vanilla's frames go by his speed with `ai[0]` 0). SyncNPC, then the news. Their camera goes to halfway between them, 24 px up (`PisPlayer.BlendCamera`), and settles there for 18 ticks under the black; the picture comes back over 30. He walks (`GuideIntroGuide.PreAI`, every machine; no AI of his own; `dontTakeDamage`; nobody can chat to him): 1.4 px/tick, 0.08 to get going, slowing at 0.06 of what is left (never under 0.3), `StepUp` for single steps, a hop of 6.5 after 6 ticks pressed against something. |
| `Arrived` | Stopped 3.5 blocks off their middle (56 px, within 4) and still, or 10 s of walking. 20 ticks later their machine opens the talk (`Dialogue.Converse`, locked). |

The player faces him from the cut on. The talk: each line moved on by "..." (`Dialogue.Scene`); the
paper comes with its line (`QuickSpawnItem`, not if they already carry one or have it on the codex
shelf); the last line has the four answers. An answer gives its kit, closes the box, tells the
server (`Done`) and lets everything go: the bars ease out, the camera walks home, Stillness ends
and the lean with it. While the scene holds them they chat to nobody (`GuideIntroQuiet.CanChat`).

**His lines** (the user's; punctuation only: a comma after the name, "don't", a period, "But
first, take this."):

1. "Oh {player}, you finally woke up."
2. "I actually don't know where you came from but, welcome!"
3. "My name is {his name} and I shall help you throughout the journey."
4. The hut hint, word for word (`Hints.GuideHut`): the user's own "wall of text" on the scouting
   and the little hut (the user called it the little temple this time).
5. "But first, take this." (the Piece of Paper)
6. "What do you specialize in?"

**The answers and kits:**

| Answer | Pillar colour | Kit |
|---|---|---|
| Close Combat | Solar (254, 158, 35) | Wooden Yoyo, Blade of Grass, Bacon x5 |
| Range Management | Vortex (0, 242, 170) | Flintlock Pistol, Gold Bow, Wooden Arrow x100, Musket Ball x100 |
| Witchcraft | Nebula (190, 30, 209) | Clothier's Chatelaine (`SealOfAClothier`, the only seal), Diamond Staff, Mana Crystal x3 |
| Behavioral Conditioning | Stardust (0, 174, 238) | Slime Staff, Leather Whip (`BlandWhip`), Dog Tail, Hermes Boots |

**The Piece of Paper's lore**, the user's, split at its commas into five verses: "To the west is
the sea, / where you can find heroes, burials and sand, / to the east is the vast jungle and
iceland, / shielding a colossal kingdom prosperous, / or at least what I remember." West and east
swap with the world (`GuideIntro.SeaEast`, set where the world is decided, in `PostWorldGen` and
`PostWorldLoad`, and sent): in a Grand world the plan's ocean end (`WorldMirror`); in any other the
Dungeon's end, since vanilla always puts the Jungle across the world from the Dungeon. Set in its
own `ModifyTooltips`, which runs before `PADGlobalItem`'s, so the text is right in whatever world
it is read.

**Verified for this** (tML 1.4.4 IL): `WorldFile.LoadWorld` runs `OnWorldLoad`, then `WorldIO.Load`
(every `LoadWorldData`), then `PostWorldLoad`, so a mod's saved flag (`WorldMirror.Flipped`) is
only there from `PostWorldLoad` on (vanilla's header, `Main.dungeonX`, is there before both);
`Player.QuickSpawnItem(IEntitySource, int, int)` spawns at
the player and, from a client, sends `SyncItem` itself (vanilla's own way for a client to give
itself something); `Entity.GetSource_GiftOrReward(string)`; `ItemLoader.ModifyTooltips` calls the
ModItem before the GlobalItems; `Main.HoverOverNPCs` opens a townsperson's chat on a right click
within the player's tile range unless `NPCLoader.CanChat` is false (any GlobalNPC's false wins),
and its only other caller is the smart interact; `NPC.VanillaFindFrame`, town branch on the
ground: no state at `ai[0]` 0, so standing at `velocity.X` 0 and walking otherwise,
`spriteDirection` from `direction`; `NPC.UpdateNPC_Inner` adds gravity after the AI whether or not
PreAI let it run; `NPC.NewNPC` names a townsperson (`GiveTownUniqueDataToNPCsThatNeedIt`);
`Player.GrabItems` is not behind `noItems`, so a held player picks the gifts up; WorldGen's Reset
pass puts `jungleOriginX` on the far side from `dungeonSide` (normal and remix); every `ItemID`
used exists (`GoldBow`, not `GoldBowOld`).

**Reviewed** (an independent pass, against the IL): four faults found and fixed. The paper's sea
was read in `OnWorldLoad`, before `WorldMirror`'s side had loaded, so half of all Grand worlds would
have had it the wrong way (now `PostWorldLoad`). `/guideintro reset` in the middle of the talk left
the locked panel up (the world had forgotten the Guide, so the panel was not shut) and the last
answer could then give a second kit (the panel is now shut by the NPC it was opened on, and an
answer gives nothing unless the scene is still the player's). A player not on their feet within ten
seconds of the count was held anyway, which in water could drown them (never forced now). A death
mid-talk in multiplayer had the panel put back every tick until the news came (given up at once).

**Not yet confirmed in game:** all of it, and multiplayer in particular.

**Open:** the numbers that were mine (15 tiles off, 3.5 blocks, the walk's pace, 20 ticks before
he speaks, "..." to move on). And the hut hint says "On the trail seawards", but the Blood Shrine
(the orb's) is built at the nearer end of the spawn's forest, which in a Grand world is the snow
side, east of the spawn with the sea west; the paper now says the sea is west. The line or the
shrine should move (`RELEASE_TODO.md`). *(Settled: the user had the shrine moved, §16.21.)*

---

## 16.21 Later in session 7: the shrine to the sea, the paper's Grand map, the other world sizes

The user, on the Guide's hut line sending the player "seawards" while the Blood Shrine stood on the
snow side: "move the shrine, also make the grand world text very convincing (state the additions
and lore structures)". Then: "Color the other world options pale also, append to the string of the
other world details: (does not include the Pistol And Dagger's full experience)".

| Work | Where | Notes |
|---|---|---|
| The shrine, toward the sea | `Content/Structures/BloodShrinePass.cs` | Still at the end of the spawn's forest, but on the sea's side, the side the Guide's paper calls the sea (`GuideIntro.FindSeaEast`, now `internal` so generation can ask it): in a Grand world the desert's edge, about 1600 tiles from the spawn toward the ocean (it was the snow side, about 525 the other way); in any other world the Dungeon's side. That side is tried on level ground and then on ground twice as rough before the other side is looked at, and a shrine that has to go the other way says so in the log. `/struct update` runs the same pass. |
| The paper, in a Grand world | `Content/Items/LoreItem/PieceOfPaper.cs` | Thirteen verses, his map of the planned country in the order it is walked from the spawn, the user's words carrying it (below). Any other world keeps the user's five verses as written. |
| The kingdom hint's direction | hjson `Hints.GuideKingdom` ("Far {0}, ..."), `GuideHints.GetChat` | Was "Far east" in every world; in a world built the other way round the kingdom is west. Now east or west, across the world from the sea. |
| The other world sizes, pale | `ExtraLargeWorld.cs` (`PaleTheOthers`, `SayWhatTheyLack`, `OneLineAgain`), hjson `UI.WorldSizeNotFull` | Small, Medium and Large: their names (vanilla's cyan to green) washed 70% toward (200, 200, 200), beside the Grand's gold. Their descriptions get a second line under vanilla's, in pale grey (`[c/b4b4b4:...]`, as the Grand's "(Recommended)" is gold): "(does not include the Pistols and Daggers' full experience)". Vanilla's panel holds one line at 0.82, so the two lines are fitted into its 40 px by the font's own `LineSpacing` and the wider line's width, never over 0.82, 2 px from the top; anything else hovered gets vanilla's one line back first. |

**The Grand map** (sea west; east and west swap with the world):

> To the west is the sea, where you can find heroes,
> burials and sand. On the way there:
> a little hut, where the trees round here give out,
> the desert, then a stretch of ground gone sick,
> an old keep with an old man at its door,
> and graves on a hill above the beach, one of them open.
> To the east is the vast jungle and iceland:
> the ice first, not far at all, then the jungle,
> a field where every tree is a stump,
> a mile of ash where nothing calls,
> and a white wall taller than any tree ever grew,
> shielding a colossal kingdom prosperous,
> or at least what I remember.

*[Superseded: the user has since rewritten these lines by hand, eleven verses now. As they are:
§16.23.]*

Each line is a thing on the ground, checked against the code: the Blood Shrine at the end of the
spawn's forest (moved, above); the desert band, then the world's evil (`Layout`, held to their
bands by `BiomeBands`); the Dungeon at the seaward end with vanilla's Old Man at its door; the
graveyard hill behind the ocean's beach, which in a Grand world always takes the ocean side, and
its open grave (`GraveyardPass`); the snow 525 tiles from the spawn, then the jungle; the Cleared
Ground, cleared but for the stumps (`GraceCleanupPass` keeps rooted trunks); the Wasted Mile's ash
(`WastedMilePass`, "where nothing calls" is the Clothier's word for it); the Curtain, 160 tiles of
white marble banded in gold (`GraceBuilder.Curtain`; "taller than any tree ever grew" is his own
kingdom hint). What he could not have seen is not in it: the Ball under the beach (the plan wants no
hint of it on that walk), the Sealed Pocket, the blade under the spawn, the Dry Shore past the
kingdom. "Heroes" is left the user's word. Names the player cannot have yet stay out: no Grace, no
Curtain, no Mile.

**The mod's name** in the new line is written as `build.txt` has it, "Pistols and Daggers'"; the
user typed "Pistol And Dagger's".

**Verified for this** (tML 1.4.4 IL and the embedded en-US localization): vanilla's three size
buttons are `GroupOptionButton<WorldSizeId>` built with Cyan, Cyan-LimeGreen and LimeGreen, and the
colour goes only to `_title.TextColor` (`DrawSelf` never touches it); `Description` is a public
field; `UIWorldCreation.ShowOptionDescription` sets `_descriptionText` from the hovered element's
`Description` only when there is one, `ClearOptionDescription` to `UI.WorldDescriptionDefault`;
`UIText.SetText(LocalizedText)` keeps the last scale, so the scale is put back before vanilla
writes; the description panel is 40 px, text 0.82, padded 20 each side and 6 from the top, not
wrapped; `ChatManager.DrawColorCodedString` breaks lines at `\n` by `font.LineSpacing * scale`.
Vanilla's descriptions: "Ideal for a quick, single player experience.", "Bigger and better for
prolonged exploration, or bringing a friend.", "Super-sized for long term progression and multiple
players."

**Not yet confirmed in game:** the two lines in the panel (the fit is computed from the font, but
not seen), the shrine's new place in a fresh Grand world, the paper's lines in the tooltip.

---

## 16.22 Later in session 7: the Lore Bible: the Horseback Fool, the Megashark's looks, Amalgaermis's name and tail

The user: "update lore on the horseback fool, her insperation and a detailed description of her
and her horse and lance, same as mors", then "and megashark looks also", then "also amalgaermis
has a name now, and add the butt hole detail not too graphic tho". Lore only: the one file with
content changed is `Terraria_Mod_Lore_Bible_ver_1.1.md`. No code, no hjson.

| Where in the Bible | What |
|---|---|
| §XIX, after the Cupbearer | New `#### The Horseback Fool`. Built facts first, each [LOCKED — built] and read off the code and the hjson, her lines as the user wrote them: her names, where and when, the road, the fight, the fall, spared, finished, what each ending hands over. Then `##### Her Inspiration`, `##### How She Looks`, `##### Triunfo`, `##### The Lance` (the last three "[LOCKED — the read. The sprite is built.]", as Mors's is), `##### Her Place in the Book` (readings, PROPOSAL), a "Deliberately unresolved" list, and one [OPEN] that wants deciding (below). No section was renumbered. |
| §XXII Megashark | New `### How It Looks`: the sprite (the animal, the machine, nothing joining them), what happens to the picture (the rise, heat, red, the spin, the sight, the damage it carries, the two hand-overs), how it dies, what it leaves. |
| §XXI, `#### The Abomanation` | Now `#### Amalgaermis`, and the picture's alt text: the book uses the name, and "The Abomanation" is kept as the code's word. The line under the name quoted as the game has it. [OPEN] who named it. In *How it looks*, the tail: bare, with the opening at the end of it, five sentences and one PROPOSAL reading. *How it dies*: "The bare end goes first." |
| §XXI, `#### What It Leaves` | Brought up to the build. How the incantations are got is no longer a proposal: Essence of the Fouled, 20 to 25 a kill to each player, 20 and 29 with a Book at the Makeshift Altar. Everhunger's flavour line is the hjson's, the user's: *'Even in soul they keep biting.'* New entry: Joint Arms of the Defiled, whose tip is that tail end. |

**Her inspiration is my reading, not the user's word.** The brief that asked for her is not in what
I can read any more. Taken as Don Quixote from what is built: Dona for Don (Doña), the *Qui* of
both her names, the Locket of a *Book-Crazed* Girl, *a knight who was never knighted*, a donkey
with a grand name (Triunfo, triumph; in Cervantes the donkey is the squire's), the player taken for
a monster as the windmills were for giants, and *Me rindo*. The Bible marks it as a read to be
corrected. The in-world half (what made her) is the locket's paper.

**Measured for the looks** (the PNGs, art at 2x, 16 px a block):

- Her: canvas 42x60; standing 22x48 (three blocks), hair 22x20 (the top two fifths of her), face
  18x16, one face picture for standing, riding and lying. Black hair with a violet cast, tan skin,
  one eye, pink on the cheek row, blue tunic with a navy outline, dark brown trousers, tan boots.
- Riding: `DonaRig.Seat` (58, 40) on a frame whose hooves stand at y 80, `RidingSeat` (21, 46),
  hair top at y 14: the top of her hair is 72 px over the ground, Triunfo's ear tip 64.
- Triunfo, frame 0: 76x64. One ear, mane near black streaked brown, eye (15, 87, 39) with a white
  glint, white above the two near hooves only, saddle red-brown with tan and gold (the only gold
  hues in any of the three sprites), no bridle or rein pixels, two rust red marks (by the root of
  the tail, low on the breast; missing from frames 3 to 5, 8 and 16 to 19). Frame 8 is drawn and
  used by no gait.
- Lance: 22x76, as long as Triunfo's frame 0 and 1.58 of her height; needle point, cone to a
  guard at rows 54 to 61, brown grip at rows 62 to 67 (`DonaRig` calls it leather), steel cap.
- Megashark: 70x28, one frame. Hide (68, 34, 37) to (167, 123, 127), cream belly, olive under it,
  barrel from x 48 (about 22 px of 70), grey top line, alternating vents, black underside, one
  eye pixel at (40, 16).
- Amalgaermis's tail: 60x80, five colours, none of them bone. Two dark marks inside the flesh in
  the outline's colour: (46 to 49, 48 to 49) on the flank, and (30 to 31, 66 to 67) with (28 to
  31, 68 to 69) dead centre at the tip. The Bible says "at the end of it" and "a few dark pixels",
  which is true of either; the whip's sheet has its opening at the far end.

**Reviewed** (an independent pass over the new text against the code, the hjson and the pixels;
everything it found was corrected before delivery). What it caught, for the record: the title is
never drawn on the bar, only under the name while the name goes up, and on the dialogue tab
(`BossPlate`: "The title exists only in flight"); the locket's flavour line and its verses are
never on screen together (the verses are the Shift view); the red wash cannot show in phase 3
(`heatGlow` is pinned at 1 and red needs `rageGlow > heatGlow`); the sight follows the player for
45 of its 90 ticks and then stops; the Spin attack turns the whole sprite; the white is on the
near hooves only; the whip throws its own blood, it does not make the target bleed; one kill's
essence writes the Court and not Everhunger; "Doña"; and the two below.

**Found on the way, not fixed (code, and not asked for):**

- *Her bar after the fall.* `DonaQuinn.cs` sets `NPC.boss = fighting` with the comment "gone from
  the fall", and §16.18 says the same. Nothing that draws the bar reads that flag: vanilla tracks
  any active NPC with a boss head (`CommonBossBigProgressBar.ValidateAndCollectNecessaryInfo`:
  `active` and `GetBossHeadTextureIndex() != -1`; `BigProgressBarSystem.TryFindingNPCToTrack`
  asks nothing more), and `BossPlate.Track` holds the bar while she is active with life over 0.
  Read off the IL and the source, not seen in play: an empty bar probably stays up through the
  plea and the talk. The Bible does not say the bar goes. In `RELEASE_TODO.md`.
- *The Megashark's other drops.* The comment says the kill is worth one gun and nothing else, and
  `NPC.value` is 0, but it is a boss with no `BossLoot` override, so vanilla adds 5 to 15 Lesser
  Healing Potions and 5 to 9 hearts (`NPC.DoDeathEvents`: `if (boss)` then
  `DoDeathEvents_DropBossPotionsAndHearts`). The Bible says only that it pays no coin. In
  `RELEASE_TODO.md`.

**Open, for the user:**

- Whether Don Quixote is what she was drawn from.
- §XIV has nothing stay dead before the restoration, and no exception for a player who kills on
  purpose. She is finished before hardmode and never met again. The Bible flags it and offers the
  reading that costs nothing to build: she gets up somewhere the player is not and does not come
  back for the locket.
- Amalgaermis's body sprites carry eyes of several colours (blue, green, magenta, one or two a
  segment). *How it looks* says of the head "There are no eyes" and does not mention these.

---

## 16.23 Later in session 7: Amalgaermis: four things to break the chase, and something wrong with it

The user: "For Amalgaermis, its moveset is too simple, and repetitive, even for legends: Check
fables and add this. When its underground, there is an indicator for where it(the head) is on the
ash sand(dust thrown up). Whenever it transition from frame 2 and frame 3, it creates some sparks
and blood outward, like a quick bite. Need more SFX(again i love the initial screech when u summon
him), like the short screetches variations and maybe a loud cry with rythm when they about to do
whip. some not so frequent whimpering after be hit can do too. Phase 1 and 2 move and behaviours:
1, Puke out projectiles with textures of bones x 5-8, skull x2 (split into 3 bone each), hands x2
(splatter on impact) 2, It lays tangential on the hill and use its hands to throw ash balls(like
sandbal) up ward(dust on impact dont create block, will collide with tiles and platforms) 3,
Frequent slow down and shakes of the segments and bleed out. can be signified by a short
screetch(create) 4, The Rush to the center of the player is more frequent than above them. Phase 2
specific move and behavior: 1, it blows steams out of each connection of its segment and bleed out
and heal for like 500 HP on expert legends(scale this back) If you can do anything to improve the
visuals of the other, please do. Make it beautiful, in pain and blood thursty(mix of jurassic world
raptor and shin godzilla) take yo time". Then: "If u have to add any shader, effects or particles
please add"; "the texturues can js reuse from vanilla or from already existing sprites( dont need
to create any new)"; on Item_154, Gore_42, Projectile_21, 471 and 532: "these bones are good alr";
"the puke should be like how it choke on something and splat splat splat! Should have indicator
for this like slow dawn and curve up from the ground I think / interval like 0.7ss then 0.3s"; and
"some", over a note that a screech frightens most when it is rough (30 to 150 Hz), sits at 2 to
5 kHz around 3 kHz, and breaks.

Fables was read for the idea of the ground showing where a burrowed head is (its Desert Scourge)
and nothing was taken from it.

| Work | Where | Notes |
|---|---|---|
| The head, in three files | `AbomanationHead.cs`, `AbomanationHead.Moves.cs` (new), `AbomanationHead.Show.cs` (new) | `partial`. The chase, the rush, the whip, the arrival and the end stay in the first. `Moves` is what breaks the chase and what decides between them. `Show` is everything only seen or heard: it decides nothing, is never sent, and a server runs none of it. |
| What decides | `Moves`: `RollMove`, `TryMove`, `AfterMove`, `Clocks` | `RushGate` (ten seconds flat) is gone. Each time the chase begins a move and a gate are rolled: 270 to 390 ticks (180 to 270 in the second half; Legends 0.85 of either); rush 5, puke 3, ash 3, and whatever it did last at 0.3 of its weight. The vent is on a clock of its own (below). A rush starts once the head is below you; the puke and lying down once the head is under the ground and staying there (`Buried`); 240 ticks late, any of them starts anyway. The whip follows any finished move, by the rules it had when it only followed a rush. |
| The rush through you | `Moves`: `StartRush`, `ThroughStart`, `Through`; `AbomanationHead.Pace` | 68% of rushes (78% in the second half; always, for Legends' far rush). Below. |
| The spasm | `Moves.Clocks`; `Show.SpasmBegins`, `Shiver`, `ShowPart` | Its own clock, in the chase only, never within 45 ticks of a move. Below. |
| The puke | `Moves`: `StartPuke` to `Spew`, `HeaveSpit`, `Spit` | Below. |
| Lying down: the ash, the vent | `Moves`: `LaySite`, `StartLay`, `LaySeeks`, `Lies`, `ThrowAsh`, `Vents`, `VentHeals` | Below. |
| Back under | `Moves`: `Sink`, `Sinks` | A driven turn to straight down, only ever the one way, 3.2 times `TurnSpeed`, half as fast again once it points down. Under at 32 px below the surface, then the chase, and the whip if it is due. |
| The ground over it | `Show`: `Tremor`, `Surface`, `Break`, `Kick` | Below. |
| The jaws | `Show`: `Snapped` (called from `FindFrame` with the frame that is drawn), `Snap` | Wide to shut on consecutive frames is a bite whatever shut it: 8 `SparkShard`, 12 `BloodDroplet`, a `BloomGeneric`, the snap sound, 32 px ahead of the head's centre. It happens at the top of a rush, at the end of the spasm's cry, as the whip lets go, on every heave, and when the vent ends. (Eating an animal goes from parted to shut, and throws its own blood.) |
| Its voice | `Abomanation.cs` (nine `SoundStyle`s), `Content/Sounds/Abomanation*.ogg` (20 files) | Below. |
| The body | `Show`: `Shiver`, `Swell`, `Glow`, `ArmsOf`; `AbomanationArms.ArmPose`, `Hand`, `ArmNumber`; `AbomanationChain.Follow`; `AbomanationBody`/`Tail.DrawWhole` | Every part asks the head each frame. Drawn off its place by a few pixels when it shakes (nothing moves); drawn bigger where a lump is on its way up the neck, and all over as the vent's pressure builds; an added red pass in the second half (an ember that travels down it) and when it vents; arms thrashing (`Frenzy`), and raised and flinging on the upper side of a body that is lying down. |
| What it throws | `AbomanationSpew.cs` (new): `AbomanationBone`, `Skull`, `Hand`, `Ash` | No art of their own, by the user's word: vanilla's Bone (21), Skeleton's bone (471), Bone Glove's (532) and the Bone item (154); Gore 42 for the skull; two of the animal's own arm sprites for the hand; vanilla's falling ash ball. They stop on platforms as on ground (`TileCollideStyle`, `fallThrough = false`) and leave no block. |
| Steam | `Content/Particles/SteamPuff.cs` (new) | Wears `FlameSmoke.png`'s four puffs, which are stored premultiplied as the pass wants. No new texture. |
| The ground's colour | `Content/Particles/GroundWave.cs` | `FogColour(int, int)` made public for the dust over the head. |
| Testing | `AbomanationCommand.cs`, `Moves.Force` | `/abomanation rush`, `lunge`, `puke`, `ash`, `vent`, `spasm`, `whip`: its next move, started the way a fight would start it. |
| Words | hjson | `DeathText.AmalgaermisSteam`; `Projectiles.AbomanationBone/Skull/Hand/Ash.DisplayName` (Somebody's Bone, Somebody's Skull, Somebody's Hand, A Handful of Ash). |

**Two rules hold it together.** *Who decides:* everything rolled, and every move's beginning, is
the server's (or the one player's), and is sent (`SendMoves`: the kind of rush, whether its line
is set, the side, the way, the stage, ash or vent, the ground line, how far it has crawled, where
it faces, the way it sinks, where you stood, the spasm). A client never starts a move. *Who
steers:* the head steers wherever the long way round is fine, and is driven where the shape
matters (the curve up, the crawl, the turn back under). Turns tighter than an attack's ordinary
one are only made under the ground, and only once the whole body is under it.

**The rush through you.** The old rush climbs to a point over your head and reaches it by a wide
turn: measured, it passes a standing player about nine tiles to one side. It is still there. The
new one dives 520 px to the side it is on and 620 below you, at 1.25 of its pace, turns hard down
there (3 times `TurnSpeed`, until it points within 0.15 rad of where you were a third of a second
ago), and comes up at that. Its line is set when it is within 150 px, or when it has pointed at
you and is now getting further off, or after 150 ticks; it then carries on 260 px past and falls.
In the simulator it passes within 1 to 7 px of a player standing still and 70 to 140 px behind
one who runs steadily. Its tell is its own bark (vanilla's `Roar` is no longer played there).

**The spasm.** Every 300 to 480 ticks of chase (210 to 360 in the second half; the first 150 to
300 in): 70 to 110 ticks. It yelps with its mouth wide and bites on the end of it; speed and turn
both fall to 0.4, eased over 14 ticks, so it slows on the circle it was on; every part is drawn
shaken up to 3 px; every joint that has air beside it bleeds; the arms thrash.

**The puke.** To one side of you, ahead of you if you are running. Stages 0 and 1: to a run-up
point and along it to where the curve starts, both 44 px under the surface. Stage 2: 18 ticks
slowing along that line, mouth open, the ground heaving over it: the tell. Stage 3: a driven curve
of radius 100 px up out of the ground, at 0.68 of its pace, until the head stands 176 px over the
ground on its own neck. Then the choke, 46 ticks (Legends 38): three catches at ticks 2, 16 and
30, each jerking the head back, a lump going up the neck, the throat sound timed to them. Then
three heaves at ticks 1, 43 and 61: seven tenths of a second, then three. Each throws the head
back, mouth wide 7 ticks and shut 9 (which is a bite, with its sparks). Bones in all three, 5 to 8
in all (Legends 6 to 8); a hand in the first and the last; a skull in the second and the last. The
first two are aimed at you, the last at where you are going (half of the way there; Legends 0.8).
One bone of each heave is dead on, the rest within 96 px. A skull breaks into three bones where it
lands; a hand bursts, and for 3 ticks hits anything within 52 px. 22 ticks after the last heave it
goes over forward and under.

**Lying down.** Only where there is ground for it (`LaySiteOn`: from 6 tiles out to 41, no step
over 3 tiles, within 22 of your feet, 8 tiles of air over it). It goes to a point 393 px under the
ground and 51.5 tiles out from where you stood; up from there to 40 px under the surface at 39
tiles; runs in under the surface to 26 tiles, the ash heaving over it; and from there crawls on
the surface at you (5.5 px a tick), barking as it comes out, until it is 12 tiles off. The body is
laid on the ground behind it (`AbomanationChain.Follow`: each part within 90 px of its own ground
sinks 0.3 of the way onto it a tick). In the simulator, 9 to 16 of its 16 parts are out when it
stops, 12 in the middle case.

- *The ash.* The arms on the upper side of every part that is out fling in turn from the neck to
  the tail, 3 ticks apart, each winding back 14 ticks. Two waves (three in the second half; one
  more in Legends), 66 ticks apart. A ball leaves upward at 10.5 to 13.5 px a tick and comes down
  somewhere within 26 tiles of you; one arm in six throws at where you are going.
- *The vent,* second half only: first owed 480 to 720 ticks into it, then 1500 to 2100 after
  each (Legends 0.85); owed, it goes ahead of whatever was rolled; with nowhere to lie, or given
  up part way, it is owed again 600 later. 50 ticks swelling and glowing, 100 of steam from every
  joint that has air beside it (150 px long, 34 wide), 20 settling. It gets back a sixty-fifth of
  its full life in five parts, never past the line the second half began at: 246, 400, 553
  (classic, expert, master) and 307, 500, 692 in Legends, which is the 500 asked for.

**The ground over it.** While the head is under the ground and within 60 tiles of the surface,
the top tiles over it throw their own dust (`WorldGen.KillTile_MakeTileDust`: ash where it is ash)
in a band 5 columns either side: more the shallower and faster it is and the more the move means
it, with now and then a puff of `DustFog` in the ground's colour. Shallower than 84 px the tiles
themselves heave as it passes (`LiftedTile`), each column once. Coming up under this machine's own
player, the screen takes it. Where the head goes through the surface the ground bursts; on an
attack (the rush, the fall, the puke's rise, going back under, coming out to crawl) a
`GroundWave.Slam` goes out from the place with a low boom. Each part kicks a little up as it
goes through, and as it rises out of the ash to lie.

**Its voice.** Twenty files, made here from the throat the arrival's screech was made from
(kept with the mod in `Claude outputs/amalgaermis_sounds/`, with a README: `synth.py` and
`presets.py`, the throat; `voices.py`, `fx.py`, `build.py`; seeded, so it makes the same files
again; `ffmpeg -c:a libvorbis -q:a 8`, mono, 44.1 kHz). After the user's note the short ones were
made rougher (beating 34 to 148 times a second) and brighter: the share of their energy between 2
and 5 kHz is 4% in the screech, about 19% in the barks, 28% in the yelps, 32% in the whip's cry.
Levelled against the screech (active RMS -13 dB). **They were measured, not heard:** nothing here
can play a sound.

| Sound | Files | When |
|---|---|---|
| Bark | 3, 0.5 to 0.8 s | A rush turning upward (played at the player it is after); coming out to crawl. |
| Yelp | 3, 0.7 to 0.9 s | A spasm beginning. |
| Whimper | 3, 0.7 to 0.9 s | On being hit: 3 in 10, never within 300 to 540 ticks of the last. |
| WhipCry | 1, 2.55 s | The whip's loop beginning: four barks closing up (0, 0.40, 0.74, 1.02 s), then the long one from 1.26 s, which breaks as the tail lets go at 2.17 s. |
| Choke | 1, 0.86 s | The choke: catches at 0.02, 0.26 and 0.50 s. |
| Heave | 3, 0.5 s | Each heave. |
| Splat | 2 | A hand bursting. |
| Snap | 3, 0.16 s | Every bite. |
| Vent | 1, 3.14 s | The vent: 0.83 s of pressure, then the steam with a wail over it. |

**What the thrown things carry.** A hostile projectile's number is doubled by the game, then
doubled again in expert and trebled in master (`Projectile.Damage`,
`CombinedHooks.ModifyHitByProjectile`). So they carry (classic, expert) 13 and 11, 16 and 14, 11
and 9, 15 and 13, 11 and 10, through `NPC.GetAttackDamage_ForProjectiles`, and land, before armour,
for: a bone
26, 44, 66; a skull 32, 56, 84; a bone out of a skull 22, 36, 54; a hand and its burst 30, 52, 78;
an ash ball 22, 40, 60. Legends 1.3 times those. The steam is each machine's own
(`Player.Hurt` on its own player, dodgeable, general immunity): 26, 46, 68, Legends 1.3 times.

**Checked in a simulator before it was written,** kept with the mod this time, in
`Claude outputs/amalgaermis_sim/` (a README; `worm.py`, the head's steering, the chain and a height
map; `moves.py`, the moves with the code's own constants; `run.py`, film strips; `stats_lay3.py`,
`stats_puke2.py`, `guard_stats.py`; `preview.py`, which draws
`Claude outputs/amalgaermis_moves_preview.gif`, a motion sketch of the four moves in circles, not
sprites). Not the game: no caves, no platforms, a height map for ground. It found three things the
first version of this had wrong, none of which a compile shows:

- *Lying down came out anywhere.* The first approach ran out under the ground, turned up and
  over, and settled onto the ground line. Over 180 starts it surfaced anywhere from on top of the
  player to 78 tiles off (36 in the middle case), gave 25 up, and in 29 the head went more than
  150 px into the air and looped before it lay down. Its settling test also measured "level"
  against the horizon, so on a slope it never passed. Replaced by the two points and the run in.
  Now, over 160 starts on four kinds of ground with the player standing: out at 25.6 to 26.0
  tiles every time, 82 to 202 ticks after the move began (169 in the middle case; 138 in Legends'
  second half), no part of it in the air when it stops, none given up. With the player running,
  24 of 360 are given up because the player has gone more than 80 tiles ahead of it.
- *A tight turn folds the chain.* A part only follows the part ahead, at its distance. Round a
  turn tighter than about two links the chain folds like a rope pulled back along itself: the
  fold travels at half the head's pace and everything behind it stops where it is. Under the
  ground that is not seen. If the body was still coming down out of a leap when the move began,
  it was a length of tail hanging in the sky: in 59% of pukes against a standing player, four
  parts in the middle case, through the whole of the choke. Three things, together: a move
  begins from under the ground (`Buried`); on its way it turns no tighter than 1.5 times
  `TurnSpeed` while any part is in the open air, and takes a long way round by way of down
  (`bodyOut`, `MoveSwing`; given up on after 200 ticks, for places with no ground to get under);
  and once the head has stopped, a part left in the air above the part it follows comes down
  (`Abomanation.Sag`, 6 px a tick, in `Follow`). After: 3 of 160, one part each.
- *What it costs.* The puke takes longer to arrive: 149 ticks in the middle case from the move
  beginning to the slowing, where it was 73. Most of that is the body following the head into
  the ground. A move rolled while the head is in the air waits 13 ticks in the middle case, 217
  at most. Shorten `MoveGateLeast`/`Most` if the fight feels slower for it.

**Verified for this** (tML 1.4.4 IL): `Projectile.Damage` doubles a hostile projectile's damage
after `Main.DamageVar`; `CombinedHooks.ModifyHitByProjectile` multiplies by
`GameModeInfo.EnemyDamageMultiplier`; `NPC.GetAttackDamage_ForProjectiles(normal, expert)`;
`ModProjectile.TileCollideStyle(ref int, ref int, ref bool fallThrough, ref Vector2)`, where
`fallThrough` defaults true; `SoundStyle(path, numVariants)` loads `path + "1"` to `path + "N"`;
`Player.Hurt(PlayerDeathReason, int, int, bool pvp, bool quiet, int cooldownCounter, bool
dodgeable, float, float, float knockback)`; `Main.instance.LoadProjectile`/`LoadItem` are public;
`NPC.IsABestiaryIconDummy` is a public field; `Projectile.Resize` keeps the centre;
`DustID.Blood` 5, `Bone` 26, `Ash` 36; `ImmunityCooldownID.General` -1. `ImageIO.ToRaw` copies
straight RGBA and does not premultiply.

**Reviewed.** An independent pass over the first version found no fault in the two rules and
seven smaller things, all fixed before delivery: the first heave played twice on a client; a vent
given up stayed owed and was tried again at once; the crawl's "near" was measured from the head
whichever side of it you were; a skull's bones carried the classic share in expert; the vent's
five parts lost 1 to 3 life to rounding; a hand's burst could happen twice or not at all on a
client; a spasm kept shaking an animal that was leaving. The second version (the approach, the
air rule, the sag) was checked by me alone, against the simulator and by reading the diff.

**Mine, not asked for** (all in `RELEASE_TODO.md`): the steam scalds (`Abomanation.VentScalds`
turns it off); the hand's burst has a reach; the whip may follow any move, not only a rush; the
rush's tell is a bark, not vanilla's roar; a move waits for the head to be under the ground; every
number above that the user did not give.

**Found on the way, not fixed:**

- `Content/Particles/DustFog.png` is stored straight (white, with the shape only in the alpha)
  and drawn in the particle handler's premultiplied pass, so its colour does not fall away with
  its alpha and the puffs come out flatter than they were drawn. King Slime's slam, the Queen
  Slime and Cold Call use it as it is, so it was left: premultiplying the file changes all of
  them.
- `PassAim` rolls its overshoot on every machine (older than this work).
- `PieceOfPaper.cs`: the user has rewritten the Grand map since §16.21 (below); the comment
  over the class still describes the earlier lines (the graveyard, "the vast jungle and
  iceland").

**The Piece of Paper, as the user now has it** (Grand world, sea west):

> To the west is the sea, where you can find heroes,
> burials and sand. On the way there is
> a little hut, where the trees round here give out,
> the desert, then a stretch of ground gone sick,
> an old keep with an old man at its door,
> To the east is the vast iceland and a restless jungle
> then a field where every tree is a stump,
> then a mile of ash where you can hear atrocities,
> and a white wall taller than any tree ever grew,
> shielding a colossal kingdom prosperous,
> or at least that's what I remember lol.

**The locket, as the user now has it** (`LocketOfABookCrazedGirl.cs`):

> A rusted gold plated locket, with paper folded small inside.
> "Dear my lovely daughter, I know you will be a shining knight one day."
> It was read until the creases went soft.

**The Lore Bible** (this delivery). §XXI *Amalgaermis*: *How it fights* brought up to the build
(the ground, the rush through you, the spasm, what it brings up, lying down, the mouth, its
voice, the steam; Legends; the whip after anything), as [LOCKED — built], with two readings marked
PROPOSAL and a line added to the [OPEN] about the hands. §XIX: the locket's verses as above in
the four places they are quoted, and the two sentences that leaned on "It was never signed"
reworded to what the item still says, which is that it names nobody.

**Not yet confirmed in game:** all of it. Riskiest: the sounds (never heard); lying down on real
ground with caves under it or buildings on it (the simulator's ground is a height map); the
moves in multiplayer (the rules are kept, but nothing has run); the arms' fling against the
balls leaving the hands; how the steam and the red pass read against the Mile's grey.
