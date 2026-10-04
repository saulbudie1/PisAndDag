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
  *[Found wanting later in session 7: this passes a sky island, which is dirt on cloud and solid all the way through. The Blood Shrine used the same test and was built on one. The shrine's test is changed; this one is not. See §16.27.]*
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

---

## 16.24 Later in session 7: the chopped forest: its floating islands off and its giants cut; and the shrine's chest

The ask, in the user's words, as it came, with map screenshots and a picture of a stump:

> for the chopped forest, try to not make these floating lands too much
> make it flatter and if there is a giant living tree, make sure its cut and removed the upper
> half, leaving only the stump of 5 blocks from the nearest ground

> stump like this , keep the under roots tho

> i like this flat part is this generated in every world? if so you did a good job and dona
> quinn will spawn here

A first make levelled the whole Cleared Ground into one field, lakes and hills and all. It was
shown as a picture and never delivered. On seeing it:

> ts too flat id like the cracks a bit but leave like half of the surface (in the middle) to
> meet and fight dame quilt

> re add lumps of dirts and ponds and cracks of earth, just remove the floating islands

> and leaf walls

> on the ground

> Add also 5 life crystal in blood shrine chest and 7 other random stuffs including bone
> x(20-25)

> why are we selecting the caves of a different biome? im only talking about the chopped forest
> on the side of the mile

A second make kept the ground as it lay but for the middle half, which it made level for the
fight. That was shown as a picture too, with the two ponds in the middle marked as filled, and
never delivered. On seeing it, with the middle of the picture cut out and sent back:

> keep these too

> too flat kills it character

**So nothing is levelled.** The pass that was delivered does two things to the Cleared Ground
and leaves the rest of it as vanilla made it: it takes the floating islands off, with the leaf
walls that stood under them, and it cuts a living tree to a stump. There is no made flat for
'Dame' Quilt: she comes where she always did (`DonaQuinnEncounter`, not changed), 30 tiles onto
the Cleared Ground from either end.
*[Superseded later in session 7: there is still no made flat, but she is now put down where the
ground is flattest and comes when a player is near that, and `DonaQuinnEncounter` is changed for
it. See §16.25.]*
*[Superseded again later in session 7: the middle of the Cleared Ground is now made calm for
her when a world is made, and she is met at the middle of it. Nothing is made level even so.
See §16.28.]*
*[And once more: of those 560 tiles only the middle 280 are made flat now, and the edges of
her ground are vanilla's. See §16.29.]*

- *"and leaf walls / on the ground"* was read with "just remove": the leaf walls on the ground
  go with the islands. The world file says what they are (below): the wall standing under a
  floating island, and nowhere else.
- *The question about the other biome's caves* was about tool work, not the mod: other
  stretches of the world file were being read as samples of what a floating island and a cave's
  roof look like, and as test ground. The pass touches the Cleared Ground and nothing else.

**What the world file said.** The answers were read out of `Worlds/Far_Tract_of_Potions.wld`
(the Grand world in the screenshots, made 2026-10-01, sea on the left) rather than off the map:
a reader written from `WorldFile.LoadFileFormatHeader`, `LoadHeader` and `LoadWorldTiles`
(version 279), with `Main.tileSolid`, `tileSolidTop` and `tileFrameImportant` taken from the IL
that fills them.

- *The floating land is vanilla's.* Its **Tunnels** pass: `maxTilesX * 0.0015` of them (18 in a
  Grand world), anywhere but the middle fifth of the map. Each is ten points 5 to 10 tiles apart,
  11 to 15 tiles over the first tile of its own column, and at each point two
  `TileRunner(strength 5-7, steps 6-8, dirt, addTile: true)`, one going each way. The result is a
  crust of soil a few tiles thick hanging over the ground on nothing, and where two runs cross,
  a lattice of them. Two are on this world's Cleared Ground: x 9374 to 9465, across its edge at
  9440, and x 9594 to 9673, with the living tree grown through it.
- *The leaf walls are the islands'.* Vanilla lays dirt wall in every column from its first tile
  down, and under a crust the first tile is the crust, so the room under it is walled; its
  Grass Wall pass later turns wall like that to `GrassUnsafe` and `FlowerUnsafe` where it stands
  in the open beside grass. In this world there are 2979 cells of those two walls standing in
  the open above the ground, and every one of them is in a column with a crust over it. So
  taking the island and what stood under it takes all the leaf wall there is on the ground.
- *The flat part the user likes is vanilla's too.* x 9465 to 9590, y 654 to 659: a level run of
  the Terrain pass, and luck. Nothing in the mod made it, no other world was promised one, and
  none is now.
- *She does come there.* `DonaQuinnEncounter` begins when a player is 30 tiles onto the Cleared
  Ground and puts her down 1150 px further toward the Mile: x 9470 and 9542 in this world, both
  on that stretch.
- *The living tree* stands at x 9653 to 9659, grown through the second island, its foot some 10
  tiles over the ground under it.
- *Three of the four Mile signs are on a sky island.* `MileSignsPass` looked for the ground from
  `worldSurface * 0.35` down, which is over the islands, and one hangs over the way in (x 10226
  to 10316, y 380 to 440). The signs are at y 417 to 422; only "god bless their poor souls" is on
  the ground.
- *The cleanup was emptying the room under the tree.* `GraceCleanupPass.Strip` takes every framed
  tile from `worldSurface - 400` to `worldSurface + 40`, which under the Cleared Ground is some
  150 rows of underground. Every other living tree in this world has its platforms, doors, chest
  and loom; this one has none of them.

**The pass** (`Content/Structures/ClearedGroundPass.cs`, new; "PAD Cleared Ground", inserted
after `Gem Caves`, so it runs first of the passes there). After everything that carves and
before everything that grows: vanilla's grass comes up afterwards on the ground an island leaves
bare, and the trees it plants are what `GraceCleanupPass` turns into the stumps, as before. Its
ground is the Cleared Ground and sixty tiles more (`ClearedStart` to `MileStart + 60`), the far
end of where `WastedMilePass` may roll its edge.

| | |
|---|---|
| A crust | In one column: ground 12 thick or less (`SlabMost`) with 3 or more rows that are not ground under it (`SlabGap`) and ground under those. Looked for from `Layout.ScanTop` down. A living tree's wood and leaves are passed over like air: a tree grown through a crust does not make it ground. |
| A run | Columns with a crust, side by side. Up to 8 columns without one do not end a run (`EdgeBreak`): they are what holds one piece to the next. A living tree standing in it ends nothing, however wide. Runs are looked for from 120 columns before the jungle end to 120 past the far end (`EdgeWalk`); one with any column on this ground is this pass's, all of it, so no half of one is left at the edge. |
| Floating land, or a cave's roof | Both are thin with room under them. The difference is where the top is. The ground either side of a run is the middle height of 6 columns of plain ground, looked for from 2 to 40 columns past each end; a straight line from the one to the other is the ground the run stands on. A run is floating land when the middle one of its columns has its top 5 or more rows over that line (`IslandRise`). A cave's roof lies level with the ground beside it, and is left: the cracks and the pits stay. |
| Taking it off | Every column of crust is cut down to what is under its room: the water if there is any, else the ground. Under the first crust anything else thin goes too while it stands 5 rows over the line, however small the room under it, which is what takes a lattice whole. A thick column standing in a run was holding it up, and is cut to the higher of the two floors beside it. Everything over the cut goes: soil, the walls that stood under the island, liquid, anything standing on it, a chest's entry with its tiles; a living tree's wood and heartwood stay for the saw. The ground under it and any water lying on that are not touched. Then the look is taken again, four times at the most: what one piece hid is seen once it is gone. |
| A living tree | Every tree with wood in this ground's columns is gathered whole, through its wood, its leaves and its heartwood wall (`LivingWoodUnsafe`; the wall is what joins a trunk to its foot across the room vanilla leaves at the bottom of it). The ground under it is found by walking under the crown from beside the tree, once from each side: each column's is looked for within 6 rows of the last one's, a row more for every column that had none, which is what keeps a crown, a branch or a root from being taken for it. Under the trunk each walk only knows its own side, and the higher of the two is taken. Where the trunk or its hollow stands at that height is a foot. A foot on this ground: the tree is cut, every trunk of a clump. All its feet past the ends: left alone, crown and all. No foot (it stood on an island that has gone): everything of it over the ground goes. |
| The cut | 5 blocks over the ground beside the foot (`Stump`), or on the tree's own top if it has one within 2 rows of that: wood lying across its hollow, the way vanilla closes the room at the foot of a trunk. Over the cut, wood is left only as splinters (one of 3 or 4, perhaps a second of 2 or 3, stubs of 1) on the columns with heartwood behind or beside them, and heartwood up to 6 rows, highest at one column and falling away from it. All leaves go. Off the trunk nothing is left above the ground. At and under the cut everything is kept: the foot, the roots, the hollow, the room, and what stands in them. |
| The foot | Within 3 columns of its hollow the stump is one piece: soil in it becomes living wood, and there is bark either side of the hollow the whole height of the stump (vanilla leaves that room open at the sides on some trunks, and the top would hang on wall alone). Further out the roots are left as roots. |

**Also changed.**

- `Layout.ScanTop`: the row `Layout.Ground` starts from, for searches that want more of the column
  than its first tile.
- `MileSignsPass`: the signs stand on `Layout.Ground`, which starts under the sky islands. Its own
  search is gone.
- `GraceCleanupPass.Strip`: a framed tile with heartwood wall behind it is left. That is the
  inside of a living tree and nothing else.

**The shrine's chest** (`PADStructureSystem.FillShrineChest`). The Orb first, as before; then 5
Life Crystals; then seven other things in no order: 20 to 25 bones, and six picked from eighteen
(`ShrineSpare`), no two the same, each with its own least and most. The user gave the Life
Crystals, the seven and the bones; the eighteen and their stacks are mine: torches 15 to 40, rope
30 to 70, glowsticks 10 to 25, Lesser Healing Potions 3 to 6, Recall Potions 1 to 3, Ironskin,
Regeneration and Swiftness Potions 1 to 2 each, musket balls and wooden arrows 40 to 90, throwing
knives and shuriken 20 to 50, bombs 3 to 8, iron bars and lead bars 5 to 12, silver coins 20 to
70, cobweb 10 to 30, candles 1 to 3. `/struct` fills it the same way. A shrine already standing
in a world keeps the chest it has.

**How it was checked.** There is no game here, so the pass was run on the user's own world.

- A Python port of the pass, written first and worked on against the world file until the result
  was right to look at.
- Then the mod's own `ClearedGroundPass.cs`, unchanged, compiled against stand-ins for the dozen
  game types it touches (`Main.tile`, `Tile`, `WorldGen`, `GenVars`, `Layout`) and run on windows
  of the world file, with a small generator the port has too in place of `UnifiedRandom`, so the
  two make the same rolls.
- **134 cases, every tile compared** (there, type, wall, liquid, half block): no difference in
  any, and no floating land left in any when the look is taken once more. They are the Cleared
  Ground as it is; 131 other stretches of the same world in both directions, as test ground and
  nothing more (forest with the four other living trees at every position against both ends,
  the two deep trees by the sea, jungle, snow, desert, the evil); and two made up: the tree with
  everything of it under its island taken away, so it stands on the island alone (all 1866 of
  its tiles go), and a chest and a door on one island and a chest under the other (gone). Across
  them 380 pieces of floating land were taken and 139 living trees cut.
- Then the Cleared Ground mirrored with the real constants, 7 rolls, and 12 more rolls on it as
  it is: the same.
- On Far Tract of Potions: both islands off, the first out to x 9374, 66 columns past the jungle
  end. 3806 cells changed, every one in the columns of the two islands and the tree (x 9374 to
  9465 and 9594 to 9675), none lower than y 661. What a walker stands on is the same in every
  other column from 9300 to 10520; the water is the same 8174 cells, the pool under the first
  island among them; no wall is left standing in the open over the ground (1059 cells before,
  553 of them leaf). The tree a stump: cut at y 652, five over the ground at 657; across 40
  rolls its wood stands 8 or 9 rows over the ground and its heartwood 9 to 11, the hollow open
  at the top; all 818 cells of its wood and heartwood at and under the cut are as they were.

A fault found by counting what had changed under the ground, and mended before delivery: under
a trunk the first ground in the column is the soil over the tree's room, some fifty rows down,
and it was being taken for a crust with room under it, so the cut ran down the side of the shaft
and took the room's wall with it.
A crust is now only looked for where no wood or heartwood is met first, the inside of a tree is
not room, nothing is cut deeper than a crust can reach under the ground beside, and what holds an
island up is not cut under that ground at all.

**Verified for this** (tML 1.4.4 IL): `Tile.ClearTile()` clears slope, half block, active and
actuated, and leaves wall and liquid; the Tunnels and Floating Islands passes as above;
`Grass Wall`, at a grass tile that has dirt or grass wall and an open cell with no wall beside
it, calls `Spread.Wall2(…, 63)` on a cell beside it that has dirt or mud wall and no solid tile,
and afterwards makes one wall 63 in ten a 65; `Spreading Grass` goes down every column and calls
`SpreadGrass` on a dirt tile when the open cell over it has no wall, which is what grasses the
bare ground an island leaves; `Wood Tree Walls` gives a tile `LivingWoodUnsafe` wall (244) when
each of the eight round it is living wood or has that wall already, down to `Main.worldSurface`;
`Main.tileSolid[LeafBlock]` is set false in Living Trees, true in Remove Water From Sand, false
again in Smooth World and true again only in Piles, so through this pass a leaf is not solid and
living wood is; no pass turns soil's solidity off; `WorldGen.genRand` makes itself when it is not
there, so the chest can be filled from `/struct` in a running world; `Chest.item` is an `Item[]`,
`Item.maxStack` and `stack` are fields; `WallID.LivingWoodUnsafe` 244, `LivingWood` 78,
`GrassUnsafe` 63, `FlowerUnsafe` 65; `ItemID.LifeCrystal`, `Bone` and the eighteen. This world's
Living Mahogany trees are all underground (y 2148 to 2369), and nothing after Living Trees was
found putting a giant on the surface.

**Mine, not asked for** (all in `RELEASE_TODO.md`): the cut moves up to 2 rows to sit on the
tree's own top; the stump is measured from the higher of the ground on its two sides; the signs'
ground; the cleanup leaving the inside of a tree; the six other things in the chest; every number
the user did not give (they gave the 5 blocks, the 5 crystals, the 7 and the 20 to 25).

**Found on the way, not fixed:**

- `GraceCleanupPass.Strip` reaches `worldSurface + 40`. Under the Cleared Ground, the Mile and
  the plain that is about 150 rows of underground with nothing framed left in it: in this world
  no pot, chest, heart or stalactite between y 700 and 845 under the Cleared Ground, and 590
  pots, 4 hearts and 699 stalactites in the 150 rows below that.
- The surface snow in this world runs x 6332 to 7306, 29 tiles from spawn (6303).
  `Layout.SnowStart` and `SnowEnd` say 6825 to 7475. Not looked into.
- `Layout.Ground`, and so `WastedMilePass`, takes a living tree's branch for the ground (wood is
  solid). With the trees on the Cleared Ground cut it no longer has one to find there.
- The built `.tmod` is 77.8 MB, and 71.9 MB of it is `Claude outputs` (91 files, 15 of them
  `.py`): `build.txt`'s `buildIgnore` does not name the folder. Adding `Claude outputs\*, *.py`
  was offered and not answered; `build.txt` is as it was.
- A crust whose underside is higher than `Layout.ScanTop` is not seen: one standing on the very
  highest ground in the world. On this world's Cleared Ground the crusts top out at y 638, and
  the highest ground in the world is at 485.
- Something standing on the ground under an island goes with the island: a surface chest
  vanilla happened to put in that room. Not seen in this world.

**Not yet confirmed in game:** all of it. It changes new worlds only: Far Tract of Potions keeps
its islands, its tree, its signs and its shrine's chest. Riskiest: a floating island the test of
five rows lets through, or a cave's roof it takes (365 pieces across one world's terrain were
looked at as counts, and some of them as pictures); the stump whose ground came down a long
way, which is the tree's foot and roots, wide, not a trunk; the hollow open on top of a stump,
which is a long fall into the room; whether vanilla grasses the ground an island leaves as the
IL says it does.

**The tools are not in the mod folder.** The world reader, the port, the stand-ins and the
comparison lived in the session's workspace and went with it. What they were is above; the
reader is forty lines once the three IL methods are read.

---

## 16.25 Later in session 7: 'Dame' Quilt's box: the flattest place, the walls, Boss Active

The ask, in the user's words:

> Check how calamity creates its boxed arena for calamitas, create an invisible one for Dame
> Quilt. its width will be the distance between the flat area (the stump also count) (prob
> half that forest). center at the middle of player and her spawn point.
>
> The camera will refuse to move past that and player will not be able to go past that screen
> edges, also destruction and build is disabled ( prob a buff called Boss Active)

Two things were asked back before anything was written, because the flat ground measured in
Far Tract of Potions was not half the forest (it was nearer three tenths), and "Boss Active"
could have meant every boss. The answers:

> Where it is the flattest, spawn her, then measure mid point and always half forest

> Only in her arena (Recommended)

**How these were read.** She is put down where the Cleared Ground is flattest. The box's middle
is halfway between the player and where she was put down, as they stand at that moment. Its
width is always half the Cleared Ground: 445 tiles, whatever the flat ground measures. The
buff is on a player only while they are inside her box.
*[Superseded later in session 7: in a world made with her ground she is put down at the middle
of it. The flattest place is looked for only in a world made before. See §16.28.]*

**How Calamity does it** (read in the user's checkout for how it works; nothing copied):

- `Systems/Mechanic/ArenaWallSystem.cs`: a `ModSystem` with a static list of boxes. A box has a
  middle, four distances (up, right, down, left), a border to draw, and a condition that says
  when it is done with; the system takes finished boxes off before the entities update, draws
  the rest after the tiles, and clears the list when the world unloads. A `ModPlayer` beside it,
  in `PreUpdateMovement`: for a box the player is inside (or within 300 pixels of), their
  position is put back inside on each side, then where they will be next tick is looked at and
  their speed cut so they land exactly on the wall.
- `NPCs/SupremeCalamitas/SupremeCalamitas.cs`: the box is 2500 pixels square (2000 in death
  mode), its middle where she is when she appears, sent with her; made in her AI, done with when
  she is gone. A player outside it enrages her.
- It does not hold the camera, and it does not stop building. `Buffs/StatBuffs/BossEffects.cs`
  (their "a boss is near" buff) is only a marker: debuff, not saved, no clock, the nurse cannot
  take it off.

**What was made.**

| Piece | Where | What it does |
|---|---|---|
| `BossArena` (static) | `Content/Combat/BossArena.cs` (new) | The box: two walls, x in the world. `Keep(left, right)` is asked every tick by whoever wants it, on every machine; it stands through the players' next update after the last ask and is down the tick after, so a boss that dies, leaves or unloads cannot leave anyone walled in. No top, no bottom, nothing drawn. One at a time. |
| `BossArenaPlayer` | same file | A player whose middle is between the walls while the box stands is held: at the end of every tick (`PostUpdate`) they are put back inside and stopped, as vanilla does at the world's edge (`Player.BordersMovement`), and given Boss Active for two ticks. Further than 320 px past a wall they are let go (a mirror, a bed). Somebody outside is left alone; somebody who walks in is held from then. |
| the camera | same file | A detour on `Main.ClampScreenPositionToWorld`, vanilla's own last word on the camera, run after it: the same sum with the box's walls for the world's edges, so nothing past a wall is ever on screen whatever the zoom and whatever any hook did to the camera before. Eased in and out over 40 ticks, so the picture does not jump when the box goes (she is thrown with the player against a wall) or when somebody walks in. |
| `BossArenaTiles`, `BossArenaWalls` | same file | `CanExplode` is false for every tile and wall in a standing box. |
| `BossActive` | `Content/Buffs/BossActive.cs`, `.png` (new) | The buff. Its `Update` sets `Player.noBuilding`. Debuff, not saved, no clock, the nurse cannot take it off. "You cannot build, mine or leave until the fight is over". |
| her box | `DonaQuinn.cs` | `boxX`, sent with her; asked for in her AI in every phase before `Buck`: from the road to the moment she is thrown. Down for the fall, the plea, the talk, and when she rides off. |
| the flattest place | `DonaQuinnEncounter.cs` | Below. `BoxHalf` = a quarter of the Cleared Ground in pixels (3560). |
| the buff's words | `Localization/en-US_Mods.PisAndDag.hjson` | `Buffs.BossActive`: its name and its line. |

**The flattest place.** Looked for once in a session, on the server, the first time it is
wanted, from where that player stands (not saved). A walk along the ground from under their
feet, the whole length of the Cleared Ground both ways: up what rises in front of the walker,
down what drops, under whatever hangs over them. Then every column is asked how far the flat
ground runs either side of it, and the one with the most on its shorter side is the place.

| Number | Value | Mine or theirs |
|---|---|---|
| Flat ground keeps within this many rows of where it is at the column asked | 6 | mine |
| and steps this many rows at most from one column to the next | 2 | mine |
| with this many rows of air over it, and no water in them | 3 | mine |
| A living tree in the way is passed over (its stump counts, as asked), for this many columns | 40 | mine |
| A tree is in a column when its wood, leaves or heartwood are within this many rows over the ground | 14 | mine |
| The place is kept this far in from either end of the Cleared Ground | 114 tiles | mine |
| She comes when a player is within this of it | 1350 px | mine |
| She is put down on it if the player is this far from it or further; nearer, this far from them on its side, then on their other side | 1150 px | the old number |
| Within this many tiles of that, the nearest place Triunfo can stand | 6 | mine |
| The box's width | half the Cleared Ground, 445 tiles | theirs |
| The box's middle | halfway between the player and her | theirs |

*[Superseded later in session 7: flat ground now steps over a small hole, 8 wide and 5 deep at
the most and 4 of them at the most; and in a world with her ground the place is the middle of
it, and the 6 rows are not asked there. See §16.28.]*

If there is nothing under a player to start the walk from (a platform, a living tree, a ledge's
edge) it is looked for again the next time, and after twenty times she comes as she did before:
anywhere on the Cleared Ground. `/dona` now says where the place is, and `/dona reset` forgets
it so it is looked for again.

**When she comes has changed with it.** She used to come the first time a player stood thirty
tiles onto the Cleared Ground, from either end. She now comes the first time a player stands on
it within 1350 pixels of the flattest place, which is what lets her be put down there off their
screen. A player who never stands that near it never meets her; one who crosses the Cleared
Ground on foot passes within it.
*[Superseded later in session 7: within 1350 pixels of her place, which in a world with her
ground is the middle of it. See §16.28.]*

**In Far Tract of Potions.** The flattest place is x 9567 (x 9566 if the walk starts from the
Mile's side): 127 tiles of flat ground toward the jungle, which is all there is of the Cleared
Ground that way, and 128 toward the Mile. The walk goes under the two floating islands this
world still has, and the living tree at x 9642 to 9668 is passed over. Coming from the jungle
she comes when the player reaches x 9483 (the check is every half second, so a runner is a few
tiles past that), and for a player standing there the box runs from x 9303 to x 9748: 137 tiles
of it lie before the Cleared Ground's start, its left wall is in the lake there (x 9285 to 9347)
and its right wall in the pond (x 9706 to 9757). Coming from the Mile she comes at x 9651 and
the box is x 9387 to x 9832.

**Boss Active stops** blocks and walls being placed, the pick, the axe, the hammer, the shovel,
buckets and the drill (that is what vanilla's flag stops), and the box stops explosions. **It
does not stop** a tile put down by a projectile (a dirt bomb, the Sandgun), liquid from a bomb
or a rocket, wire, paint, or a weapon cutting grass and breaking pots: vanilla has no one place
those go through.

**She is not held by the box.** Nothing in her fight was changed. Everything she does is aimed
at the player, who is inside, so she is never far outside it; but a charge still rides on 300
pixels past the player before she pulls up, so with the player's back to a wall she goes off
their screen for about two seconds and comes back. The other way to do it (she pulls up at the
wall) would let a player with their back to a wall never be reached by a charge.

**How it was checked.** Nothing was run in the game.

- The mod's own `BossArena.cs`, whole and unchanged, compiled against stand-ins for the game and
  run through ticks in the game's order: 39 checks, all passing. How long the box stands; a
  player walking, running and thrown into each wall; one taken far out of it; one outside who
  walks in; what can and cannot be blown up at the first and last column; the camera at four
  screen widths and four zooms (never more than half a pixel past a wall, 6 px a frame while
  following, home in 40 frames when let go); a box narrower than the screen; walking in from
  outside; being taken out; vanilla's own hold on the camera coming first.
- The flattest-place code, cut out of `DonaQuinnEncounter.cs` by a script, unchanged, with the
  `Ground` it calls, compiled against stand-ins (`Collision.SolidCollision` and `WetCollision`
  written from their IL) and run on the world file beside a Python version written separately.
  On the Cleared Ground: 28 walks (14 starting places, the world both ways round) and 112
  placements, the same in every row, kind, place and pixel. On 132 other stretches of this
  world's terrain put where the Cleared Ground is (test ground only): 261 walks and 3393
  placements, none different. On the Cleared Ground as a new world has it (the pass of §16.24
  run first): the same place, the stump passed over. The slowest walk took 16 ms.
- On those other stretches, of 1430 placements asked for she could be put down by the place in
  1429 (exactly where meant in 839, on the player's other side in 24). The one that could not
  had 18 tiles of flat ground either side; she is then put down the old way.
- The whole mod compiles: 0 errors, the 13 warnings it had.

**Verified in the 1.4.4 IL.** `Main.DoDraw_UpdateCameraPosition` ends: the camera modifiers,
`PlayerLoader.ModifyScreenPosition`, `SystemLoader.ModifyScreenPosition`, both coordinates cut
to whole numbers, `Main.ClampScreenPositionToWorld` (private static; nothing else calls it; the
hook for it is in `TerrariaHooks.dll`). That method: what is on screen runs from
`screenPosition + GameViewMatrix.Translation` for `screenWidth / Zoom.X`, and
`SpriteViewMatrix.Rebuild` makes `Translation` half the view less half the view over the zoom.
`Main.DoDraw` updates the camera before it sets the frame's zoom, so the zoom read there is the
last frame's, for vanilla as for this. `Player.Update`: buffs, then the Old One's Army's own
`noBuilding = true; AddBuff(199, 3)`, then `PreUpdateMovement`, the move, `BordersMovement`,
and `PlayerLoader.PostUpdate` last. `Player.noBuilding` is read by `Mount.UseDrill`,
`Player.PlaceThing` (before tiles and walls, after the paint tools), `ItemCheck_UseMiningTools`
and `ItemCheck_UseBuckets`, and by nothing a projectile does. `Projectile.CanExplodeTile` asks
`TileLoader.CanExplode`, and `ExplodeTiles` asks `WallLoader.CanExplode`; both hooks are
`GlobalBlockType.CanExplode(int, int, int)`. `TileLoader.CanPlace` is asked only by the
player's own placing. `Main.DoUpdate` returns before `SystemLoader.PreUpdateEntities` while the
game is paused; `DoUpdateInWorld` runs players, then NPCs, then `PostUpdateEverything`.
`Mod.Autoload` loads a mod's types in the order of their full names, and the player hooks run
in that order: `BossArenaPlayer` before `RagdollPlayer`, so a thrown body comes off the box's
wall as it comes off a wall of stone. `NPC.NewNPC` sends nothing itself.
`BuffID.Sets.NurseCannotRemoveDebuff` is there.

**Mine, not asked for** (all in `RELEASE_TODO.md`): when she comes; every number in the table
marked mine; the walls having no top and no bottom; the camera easing in and out; the box
holding any player in it, not only the one she stopped; nothing in it being blown up; a player
taken more than 320 px past a wall being let go; the buff's line and its icon (a placeholder,
`PlaceholderTextures.md`); `/dona` saying where the place is.

**Not yet confirmed in game:** all of it. Riskiest: the camera with another mod's camera work
(it runs after all of it, which is the point, but it has not been seen); her charge leaving the
screen at a wall; two machines (each makes the box for itself from what her NPC sends; never run
with two); a world made before §16.24, where the walk starting from a player who stands on a
floating island finds a different, shorter flat place; the ragdoll against the wall.

**The tools are not in the mod folder.** The stand-ins, the script that cuts the code out, the
Python version and the comparisons lived in the session's workspace and went with it.

---

## 16.26 Later in session 7: the Iron Flow: three cuts, a parry, and the parry's own machinery

The ask, in the user's words:

> create IronFlow(texture ready), a melee weapon with the ability to right click to parry :
> currently parriable projectiles are: arrows and npcs like enchanted sword, crimson axe and
> cursed hammer. Parry mechanic will be like elden ring with timing.(i think create a very
> faint hitbox for very short time idk how from soft did it).
> It also must have the holdout and parry animation like curved sword(spin and flick the wrist
> like Shamshir parry).
> Successful texture creates spark at the position of parry
> If you have any proposal on how to make parrying universal, please do.

Three things were asked back before anything was written. The answers: a parried arrow is
destroyed and a parried NPC is thrown back and stunned; left click is the held sword's own
slashes; iron tier, crafted. Then, while it was being written, four more messages, two of them
with a sketch:

> when it is during hold out(not using), its placed downward, like this

(the sword on its own, point up and to the right as its sprite is, and beside it a figure
holding it turned to point down and ahead: the handle at the hip, the hook by the feet.)

> with the hitbox parry somewhat like this

(the same figure, with a blue arc in front of it: from a little behind the top of the head,
over and round the front, down to the ground ahead.)

> with small window of chance so you have to time it

> left click still attacking tho, with 25 damage (create unique swing pattern (down, up, swing
> \full circle), each swing propel the player forward by 1 block, 0.5block then 1.5 blocks)

> no the blue is the parry box invisible

**How these were read.** The sword hangs from the hand, pointing down and ahead, whenever it is
the item held and not in use. Left click: three cuts in turn, down, up, then all the way round,
25 damage each, carrying the player forward 16, 8 and 24 pixels. Right click: a parry with
Elden Ring's own curved-sword timing, whose hitbox is the arc in the sketch, never drawn. The
25 replaces the "about 15" of the first answer; the anvil and the bars of that answer stayed.

**How Calamity does it** (the Fractured Ark, read in the user's checkout for how it works;
nothing copied): right click makes a second holdout projectile that lives 340 ticks and
parries for the first 15 of them; its hitbox is a line along the blade (`Colliding`); a parry
is that line touching an NPC in those ticks, which gives the player a moment of immunity if
the NPC does contact damage, a sparkle at the NPC and a charge for the weapon. The item cannot
be used while a blade is out. It parries nothing that is not an NPC.

**Elden Ring's numbers** (eldenring.wiki.gg, Parrying; frames at 30 a second): Parry on any
curved sword is 3 frames before it is active, 4 active, 14 after. In ticks: 6, 8, 28. The
other rows of that table, for when another weapon wants one: buckler 3/6/12, dagger 3/5/13,
small shield 4/6/11, medium shield 4/6/13, Golden Parry 4/3/16.

**What was made.**

| Piece | Where | What it does |
|---|---|---|
| `ParryTiming` | `Content/Combat/ParryTiming.cs` (new) | Three numbers of ticks: before, active, after. `CurvedSword` is 6, 8, 28. |
| `Parry` (static) | `Content/Combat/Parry.cs` (new) | What can be parried, what being parried does, the packet, the spark. |
| `ParryPlayer` | same file | A player's side: the count of the ticks, the arc while it is active, vanilla's two questions (`CanBeHitByProjectile`, `CanBeHitByNPC`) answered while it is. |
| `ParrySystem` | same file | Empties the two lists when the mod unloads. |
| `PADPacket.Parry` | `PisAndDag.cs` | The seventh kind of packet, and its `case`. |
| `IronFlow` | `Content/Items/Melee/IronFlow.cs` (new; the `.png` beside it is the user's) | The item: 25 damage, 24 ticks, knockback 5, 12 Iron or Lead Bars at an anvil. It hits nothing itself: it tells the sword in the hand what to do. |
| `IronFlowHoldout` | `Content/Projectiles/Melee/IronFlowHoldout.cs` (new) | The sword in the hand (an idle holdout, as the Greatbow's): the weapon itself. What it cuts, the step forward, the sounds, the trail. |
| `IronFlowPose`, `IronFlowLayer` | same file | The pose for the tick, and the layer that draws the sword in the hand from it (Visus's way). |
| `IronFlowMoves`, `IronFlowState`, `FlowMove` | `Content/Projectiles/Melee/IronFlowMoves.cs` (new) | Every pose of every move as curves, and the sword's state from tick to tick. Nothing of the game in the file, so it runs outside it. |
| the words | `Localization/en-US_Mods.PisAndDag.hjson` | `Items.IronFlow` (name, five lines, a flavour line), `Projectiles.IronFlowHoldout`. |
*[Superseded later in session 7: `Parry.cs` is rewritten and five files are added; `ParrySystem`
also finds what ammunition shoots, reads a config and writes a line of counts to the log; the
Iron Flow has seven lines about the parry. See §16.30.]*
*[Superseded again later in session 7: the user has since rewritten the Iron Flow's tooltip by
hand. It is one line now: "Right click to parry". See §16.31.]*

**The parry.** `ParryPlayer.Begin(timing, facing)` starts one, on the player's own machine. Its
ticks are counted at the start of each of that player's updates. With `CurvedSword`: pressed on
tick 0, it is active on ticks 6 to 13, and over on tick 42.
*[Superseded later in session 7: `ParryPlayer.Begin(timing, facing, weapon)`: the weapon that
parries is given too, and an enemy it turns aside takes that weapon's damage. See §16.30.]*

While it is active there is an arc in front of the player: the part of a circle 48 pixels from
their middle that is ahead of a line 8 pixels behind it. That is from 99.6 degrees above
straight ahead (10 past overhead), round the front, to 99.6 below. Nothing draws it. Once a
tick, after the player has moved, everything that can be parried, whose middle is ahead of that
line, and which is within the arc or will have come within it by the end of the tick (the
nearest its middle comes to the player's, along its step, less half its own size) is turned
aside; the spark is on the arc if that is where it was met, and where the thing is if it was
already inside. Behind that, vanilla's own two questions: a thing that can be parried, reaches
the player's body in those ticks, and came from ahead of the line is turned aside in place of
the hit. Ahead is judged by where the thing was before the step that brought it, so something
fast from behind cannot step over the player's back and be found at their front. What comes
from behind hits, as with vanilla's own shield parry.
*[Superseded later in session 7: an enemy must also be attacking to be turned aside: able to
hit this player now, and coming at them (at the arc) or moving at all (on their body). Shots
are as here. See §16.30.]*

**What can be parried, and what it does.**

- Projectiles: hostile, with damage, and one of: `ProjectileID.WoodenArrowHostile` (81),
  `FlamingArrow` (82), anything with `Projectile.arrow` set, a type added with
  `Parry.AddProjectile`. Vanilla's two hostile arrows do not have the `arrow` flag, hence
  their numbers. A parried projectile is ended (`Kill`): the wooden one breaks with its own
  dust and sound.
  *[Superseded later in session 7: every solid shot, by rule: vanilla's are named (29 hostile
  kinds), and another mod's is told by what it is. See §16.30.]*
- NPCs: not friendly, with damage, and one of: flown by vanilla's flying-weapon AI
  (`NPCAIStyleID.EnchantedSword`, 23: the Enchanted Sword, the Cursed Hammer, the Crimson Axe),
  a type added with `Parry.AddNPC(type, when, thrown)`. A parried flying weapon is put in its
  AI's own third state (`ai[0] = 2`, `ai[1] = 0`: 120 ticks of spinning as it slows, what a hit
  does to it in flight) and sent away from the player at 8 pixels a tick, which carries it 191
  pixels. An NPC added with its own answer gets that; one added without is staggered 120 ticks
  (`PADStaggerNPC`) and slid back 96 pixels.
  *[Superseded later in session 7: most enemies, by rule, and a parried enemy is struck,
  pushed back and staggered; `thrown` is given more. The flying weapon is as here, and open
  to the riposte while it spins. See §16.30.]*
- A parried NPC cannot touch the player who parried it for 45 ticks.
- Where it happened: a star (`CrownGlint`), a bloom, nine shards that fall, in white and
  steel (not the warning yellow, which is kept for one thing), `SoundID.Item37` and
  `NPCHit4` pitched up, and the screen shaken a little for the one who did it.

**Who decides.** The machine of the player who parried, as it decides everything else that hits
them. Alone, that is all. With a server: the thing is ended (or, a flying weapon, turned) on
that machine at once, and a packet of 13 bytes goes to the server: the kind, whether a
projectile or an NPC, which (a projectile by its identity and owner, an NPC by its place), and
where. The server looks at whether the player is alive, whether the point and the thing are
within 480 pixels of them, and whether the thing can be parried; if so it ends the projectile
(vanilla tells the others of a projectile of the server's own; for another player's the server
sends the kill itself) or turns the NPC and sends it. Then it passes the packet on to everyone
but the sender, who show the spark.

**The moves.** Angles are in the player's own frame (`VisusGrip`): from straight ahead, down
positive. The hand is `VisusGrip.FrontHand`, vanilla's own sum.

| | Blade | Arm | Ticks |
|---|---|---|---|
| Hanging | 40 below level | 60 below level, out | |
| Cut 1, down | raised through the front to 138 above and behind by a quarter of the way in; hung to 30%; down to 50 below by 62%; back to where it hangs | goes with it | 24 |
| Cut 2, up | dipped to 65 below by a quarter of the way in; up to 125 above and behind by 60%; left at 135 | goes with it | 24 |
| Cut 3, round | drawn back to 150 by 14%; then 540 degrees the same way the first cut goes, down through the front, under, up behind, over and down the front again, by 72%; to where it hangs | goes round with it | 36 |
| Parry | cocked to 60 below over the 6 ticks before; 270 degrees the other way, up through the front and over the back, over the 8 active ticks; 135 more over 6 ticks as it slows, which leaves it 25 above where it hangs; lowered over the 22 that are left | out in front; the wrist turns | 42 |

A move is begun from wherever the blade is (`IronFlowState`): each has its own path from its
own start, and a blade that is anywhere else joins it over the move's wind, the shorter way
round, before it is fast. The cut up leaves the blade raised; with nothing following it is held
there 8 ticks and lowered over 18. The cuts follow each other while the next is begun within 40
ticks of the last one ending; after that, or after a parry, the next is the first again.

Each cut hits along the blade (a line from 12 to 66 pixels out from the hand, 38 wide), over
the whole of the blade's sweep since the tick before (a step every 12 degrees or less), each
NPC once a cut, only what the player can see, knocked away from the player. Cut 1 cuts on ticks
8 to 14 of its 24, cut 2 on 6 to 14, cut 3 on 6 to 25 of its 36. The step forward is Visus's
thrust's own: where the player should be along a curve with nothing moving at either end, and
the speed that gets them there, set only when it is more than they already have.

The item is in use for exactly as long as the move takes. Its two multipliers are the ticks
meant, and a half, over what vanilla is about to divide by; the use time and the animation come
out at those ticks at every attack speed.

The sword is drawn by a player layer in vanilla's own slot for a held item, behind the front
arm. It is placed from where the player is being drawn (`PlayerDrawSet.Position`), not from
where they are, so it is in the hand in the inventory's picture of them too; and it rides the
two pixels the body rises on six frames of the walk, with vanilla's own sum for the arm. The
projectile draws only the trail: three pale shadows of the sword a tick apart, and a band over
the last three and a half ticks of the blade's way, dark under pink under a paler line.

**The numbers.**

| Number | Value | Mine or theirs |
|---|---|---|
| Damage | 25 | theirs |
| The cuts and their order | down, up, all the way round | theirs |
| The step forward | 1 block, half a block, 1.5 blocks | theirs |
| How it hangs | pointing down and ahead, by the sketch | theirs (the angles, 40 and 60, are mine) |
| What can be parried, and what becomes of it | arrows, destroyed; flying weapons, thrown back and stunned 2 seconds | theirs |
| The parry's hitbox | an arc in front, by the sketch, not drawn | theirs (48 px, and 8 behind the middle, are mine) |
| The parry's ticks | 6 before, 8 active, 28 after | Elden Ring's, for a curved sword |
| Use time | 24 ticks; the third cut 36 | mine |
| Knockback, rarity, price | 5, Blue, 20 silver | mine |
| Recipe | 12 Iron or Lead Bars at an anvil | the anvil and the bars theirs, the 12 mine |
| A flying weapon is sent off at | 8 px a tick | mine |
| A parried NPC cannot touch its parrier for | 45 ticks | mine |
| The cuts follow each other within | 40 ticks | mine |
| Raised after the cut up: held, then lowered over | 8, 18 ticks | mine |
| What a cut hits with | a line 12 to 66 px out from the hand, 38 wide | mine |
*[Superseded later in session 7: what can be parried, and what becomes of it, is §16.30's.]*

**How it was checked.** Nothing was run in the game.

- `Parry.cs`, `ParryTiming.cs`, `IronFlowMoves.cs` and `Easing.cs`, whole and unchanged,
  compiled against stand-ins for the game and run through ticks in the game's own order
  (the player's update with its questions about NPCs, the item, the player's last hook; then
  the NPCs, with vanilla's flying weapon written out from its IL; then the projectiles, moved
  and then asked about the player): 23,213 checks, all passing.
- When to press. An arrow at 11 pixels a tick is parried when the button is pressed 6 to 17
  ticks before it would have landed (a fifth of a second); at 6 a tick, 6 to 20; at 16, 6 to
  16; at 30, 6 to 14; at 80, 6 to 13, so nothing fast steps over the arc. A flying weapon
  launched from 300 pixels: 6 to 20 ticks before it would have touched. Later than 6 and the
  hit lands first; earlier and it lands after.
- From behind, never. An arrow falling on the head is parried when its middle is no more than
  8 pixels behind the player's. Two arrows two ticks apart, one press: both. A hostile
  projectile that is no arrow, a friendly arrow, a zombie: not parried.
  *[Superseded later in session 7: a zombie is parried now, and so is any hostile shot that
  is solid. See §16.30.]*
- A flying weapon parried 9 ticks before it would touch: turned on tick 33 with its middle 46
  pixels off, sent off at 8, 237 pixels from the player at its furthest, and it comes again 120
  ticks after the parry. One parry of it, not one a tick. Held on the player: it cannot touch
  them for exactly 45 ticks, and something else in its place can.
- The packet: 13 bytes, read whole at both ends; the server ends its own arrow, sends the kill
  itself for another player's, turns a flying weapon and sends it, and will not act on a point
  far from the player, a point that is no number, a thing that is far, a thing that is not
  there, a thing that cannot be parried, or a dead player.
- The moves: each starts where it says and has no jump in it; the cut up ends where the circle
  starts; the circle and the parry end where it hangs; the three cuts with the button held,
  the parry, and the lowering never turn the blade more than 44.5 degrees in a tick; any move
  begun at any tick from 0 to 45 after the cut up ends hanging. The item's use time and
  animation come out at the ticks meant at all 2,601 attack speeds from 0.4 to 3.0, for a cut,
  the circle and the parry.
- Pictures of every tick of every move, drawn from the state the mod's own code ran and placed
  with the same sums as the layer, facing both ways and under reversed gravity; an animation of
  the three cuts and a parried arrow, shown in chat. Kept in the session, not in the mod.
- The whole mod compiles against the real 1.4.4 assemblies: 0 errors, 10 warnings. **The
  baseline is 10 from here, not 13.** Three of the 13 were
  `Content/Items/LoreItem/MeleeDecider.cs`'s, and that file is no longer in the mod folder: the
  session's copy of the source still had it, and it was found by listing the folder's `Content`
  against that copy before delivering (every other `.cs` there is the same size in both, and
  the thirteen this code leans on are the same byte for byte). The copy has it no more.

**Read in the 1.4.4 assembly's IL for this.** `Projectile.Damage`, for a hostile projectile and
this machine's player: `Colliding` first, then `CombinedHooks.CanBeHitByProjectile`
(`ProjectileLoader.CanHitPlayer`, then `PlayerLoader.CanBeHitByProjectile`), then vanilla's own
`Player.CanParryAgainst`, then the hurt; an arrow is not ended by hitting a player.
`Player.Update_NPCCollision` runs for this machine's player only, and asks
`CombinedHooks.CanNPCHitPlayer` (`NPCLoader.CanHitPlayer`, then `PlayerLoader.CanBeHitByNPC`) of
every hostile NPC before it has compared the two rectangles (the NPC's with its `netOffset`
added, then `NPC.GetMeleeCollisionData`). `Player.CanParryAgainst`: the shield's ticks running,
the sign of the enemy's middle less the player's the same as the player's direction, the enemy
moving, the player not immune. The flying weapon (`aiStyle` 23, in `NPC.VanillaAI_Inner`): at
`ai[0]` 0 it launches at its target at 9; at 1 it flies, slowing by a hundredth a tick, and a
hit or 100 ticks puts it at 2; at 2 it slows by 4 hundredths a tick and spins, and at 120 ticks
goes back to 0. `Projectile.Kill` sends the kill (29, identity, owner) only on the owner's
machine. `Projectile.Update`: the damage stats, the AI, the move, then `Damage`.
`Player.ItemCheck_Inner`: the use is started, then the animation is counted down (so a use's
first tick is one of its ticks), then the item time, then `ItemCheck_OwnerOnlyCode`, which
calls `ItemCheck_Shoot` while the animation runs and the item time is none; the composite arms
are switched off there every tick. `ItemCheck_Shoot`: `CanShoot`, the item time applied, the
player turned to the cursor, then `Shoot`. `CombinedHooks.TotalUseTimeMultiplier` is the two
hooks over the use speed; `TotalUseAnimationMultiplier` is the two hooks times the use time cut
to whole ticks at that speed, over the animation. `Item.attackSpeedOnlyAffectsWeaponAnimation`
is only ever set for vanilla's own items. `Player.SetCompositeArmFront` and `Back` turn the
rotation over themselves under reversed gravity, and vanilla's own callers pass it the right
way up. `PlayerDrawLayers.DrawPlayer_28_ArmOverItemComposite` adds
`Main.OffsetsPlayerHeadgear[bodyFrame.Y / bodyFrame.Height]`, less 2 down, to where the arm is
drawn. `ModType.NewInstance` makes a new instance unless told to clone.

**Found on the way, and left alone** (all three in `RELEASE_TODO.md`).

- `MeleeDecider.cs` is gone from the folder, and `Items.MeleeDecider` is still in the `.hjson`.
- `VisusGrip.ArmRotation` and `ColdCallHold.ArmRotation` multiply the arm's rotation by
  `gravDir`, and `Player.SetCompositeArmFront` then turns it over again. Under reversed gravity
  the arm points the wrong way unless it is straight up or down. The Iron Flow passes the
  rotation the right way up, as vanilla's own callers do. The fix is to take `* player.gravDir`
  out of both.
- An idle holdout is ended every tick its owner is frozen, stoned or webbed
  (`BaseIdleHoldoutProjectile.AI`), and made again the same tick (`PADHoldoutPlayer`). The
  Greatbow's does it too.

**Making the parry universal: what is there, and what is proposed.** There already: the two
lists (`Parry.AddProjectile(type)`, `Parry.AddNPC(type, when, thrown)`), and
`ParryPlayer.Begin(timing, facing)`, which any item can call with its own `ParryTiming`. Proposed,
none of it built:

1. More of vanilla by rule, as arrows are: things thrown or shot that are solid (javelins,
   knives, darts, spears, bones). Not light, fire or magic.
2. A boss says which of its moves can be parried: `AddNPC(type, when: in that move, thrown:
   what it does to it)`. The lance at the end of 'Dame' Quilt's charge is the obvious first.
3. One parry is not enough for a boss: a count on the NPC, and it is staggered on the second or
   third, as Elden Ring's are.
4. Any weapon or shield can parry: each gives its own `ParryTiming` (the table above), and a
   key of its own for the ones whose right click is taken.
5. Something for a parry landed: the next hit on what was parried within a second is a
   critical one.
6. The yellow star already means "no shield turns this off". The same rule for the parry:
   what wears it cannot be parried.
7. A `Mod.Call` so another mod can add its own things to the two lists.
*[Superseded later in session 7: 1, 2, 3, 6 and 7 are built, and 5 as the riposte: the first
hit on a staggered enemy does three times its damage and is never a critical one. 4 is not
built. See §16.30.]*

**Mine, not asked for** (all in `RELEASE_TODO.md`): every number in the table marked mine; what
is already inside the arc when it comes up is turned aside too, and so is what reaches the body
from the front in those ticks; a parried NPC cannot touch its parrier for 45 ticks; the shake;
the cuts' knockback being away from the player; the cuts starting over after 40 ticks or a
parry; the blade being held raised after the cut up; any hostile projectile with the `arrow`
flag and any NPC flown by the flying-weapon AI counting; the two lists; the tooltip's lines.

**Not yet confirmed in game:** all of it. Riskiest: how it looks in the hand (it is 64 pixels
of sprite on a player 42 tall, so hanging down it lies over the ground by about 30 pixels); the
sounds, which have not been heard; the step forward on slopes and at ledges; two machines (the
packet, and another player's sword, which each machine runs for itself from three numbers);
reversed gravity.

**The tools are not in the mod folder.** The stand-ins, the runs, the pictures and the animation
lived in the session's workspace and went with it.

---

## 16.27 Later in session 7: the Blood Shrine that was built on a sky island

The ask, in the user's words:

> could you inspect why my world doesnt have blood shrine generated?

**It has one.** The world is The Run of Bladders (Grand, 12600 by 3000, seed 1903193645, made
on 2 October, sea to the east, spawn at x 6302, y 596). Its Blood Shrine stands at x 7973 to
7991, y 200 to 212: on a sky island, 1,680 tiles east of the spawn and 436 tiles over the
ground. Its chest, at (7980, 205), holds the world's one Game Changing Orb (the `.twld` has no
other), the 5 Life Crystals, 20 bones and the six other things. Nothing on the ground says it
is up there, and the Guide's hut line sends the player along the ground.

**Why.** `BloodShrinePass` walks back from the end of the spawn's forest (x 8044 in this world)
and takes the first footprint that is level, dry and forest. The ground there, from x 7849 to
x 8043, is a lake, and is rightly refused as wet. Over the lake hangs an island, x 7888 to
8018, y 186 to 240, and its top is level, grassy and dry but for a pond at x 7992 to 7995.
`Surface` takes the first solid tile from y 50 down that `Grounded` passes, and `Grounded`
asked one thing: are 18 or more of the 40 tiles from it down solid. That fails a ledge. An
island passes it: this one is 28 to 40 tiles thick, a row of grass, 7 or 8 of dirt and 19 to 22
of cloud, all of it solid. So the island's top was read as ground, and it was the first place
that fitted.

The log of the session the world was made in (`tModLoader-Logs/Old/2026-09-04-80.zip`) has no
line about the shrine, because the pass said nothing when it built: a world with its shrine in
the sky and a world with none looked the same from the log.

**The fix.** `Grounded` now fails a tile for a second thing: a stretch of 40 open tiles or
more that begins within 100 tiles under it. That is the sky under an island. The pass also
says where it built, in the log: `Blood Shrine: x 7826-7844, ground y 629, 1533 tiles east of
the spawn (x 6302).`

| Number | Value | Mine or theirs |
|---|---|---|
| Open tiles in a row, under a solid tile, that make it part of something floating (`IslandGap`) | 40 | mine |
| How far under the tile that stretch may begin (`IslandThick`) | 100 | mine |
| Solid tiles wanted in the 40 from it down, as before (`GroundSolidMin` of `GroundProbe`) | 18 of 40 | as it was |

Where the two numbers come from, read in the 1.4.4 IL. The Floating Islands pass
(`<>c.<AddGenPasses>b__288_19`) rolls an island's x in the middle eight tenths of the world,
finds the first tile there from y 200 down, and puts the island's middle at `Next(90, that -
100)`, then at `Min` of that and `worldSurfaceLow - 50`: a hundred tiles or more over the
first tile under its middle. `CloudIsland` lays cloud where `sqrt(dx² + (3·dy)²) < 0.4 ×` its
strength, which is 30 tiles under the middle at the most with its drift. So there are 70 open
tiles under an island's middle at the least, and the ground rising under its rim can take
some of that: 40. The thickest island in the three worlds measured, house and all, is 66 to
70 tiles: 100. Cloud is solid when this pass runs: vanilla turns `tileSolid` off for cloud and
on again inside one pass, Piles (`b__288_76`), and nowhere else.

**How it was checked.** Nothing was run in the game.

- The mod's own `BloodShrinePass.cs`, whole and unchanged, compiled against stand-ins for the
  game and run on three Grand worlds read from their `.wld` files, the copy on the device and
  the fixed one side by side. The device's copy builds at x 7973 to 7991, y 200 to 212 in The
  Run of Bladders: on the island, tile for tile where the world has it. The fixed one builds
  at x 7826 to 7844, y 618 to 630, floor on y 629, 1,533 tiles east of the spawn: on the bank
  of the lake. In Far Tract of Potions (x 4582 to 4600, y 626 to 638) and Life (x 4566 to
  4584, y 559 to 571) the two build in the same place.
- The two readings of the ground, column by column over the three whole worlds: they differ
  on 1,062 of 12,500 columns in The Run of Bladders, 946 in Far Tract of Potions and 1,127 in
  Life. The sky islands are 871, 774 and 961 of those. The rest are narrow stretches, mostly
  one to nine columns, of ground with forty open tiles close under it: the roof of a tall
  cave. In The Run of Bladders' spawn forest there are two of them, x 6938 to 6943 and x 7124
  to 7129, and neither is where a shrine was or would be. The forest's ends are read the same.
- The whole mod compiles: 0 errors, the 10 warnings it had.

**Left as it is.**

- **The Run of Bladders itself.** Its shrine is where it is. `/struct update` calls it present
  (it looks for the Orb in a chest, and finds it), so it builds no second one. `/struct` at
  the cursor would stamp another, and with it a second Orb.
- **The graveyard's ground test.** `GraveyardPass.Grounded` is the same 18 of 40, and line 356
  of this file says it "rejects floating islands". It does not, for the same reason. It was
  not changed: nobody asked, and in this world the graveyard is on the ground (x 11494 to
  11644, level 662). It is in `RELEASE_TODO.md`.

**Seen in the same log, not looked into.** `The Sealed Pocket found nowhere in the cavern
under the Cleared Ground that was not already spoken for. It is not in this world.` The Run
of Bladders has no Sealed Pocket.

**Mine, not asked for** (all in `RELEASE_TODO.md`): the two numbers, and the line in the log.

**Not yet confirmed in game:** that a new world's shrine is on the ground. The log line says
where to look.

**The tools are not in the mod folder.** The world reader, the stand-ins, the comparison of
the two readings and the picture of where the shrine is lived in the session's workspace and
went with it.

---

## 16.28 Later in session 7: 'Dame' Quilt's ground: the middle of the chopped forest made calm

The ask, in the user's words, with a screenshot of the player standing at the lip of a pit on
the Cleared Ground and `/dona`'s line over it ("The flattest of the Cleared Ground: x 2670, 28
tiles of flat ground toward the Cleared Ground's start and 29 toward the Mile"):

> this is supposedly the flat ground for dona quilt but its not flat at all, as a mater of
> fact, theres a giant hole that hinder her jousting, fix this, flat ground must be flat
> undisturbed, can have a few hole but 5  tile deep max  in this area specifically

And while it was being worked on:

> yeah hthis is too ruggedy, i like natural highs and lows but this part needs to be calmer

**What was there.** The world is The Run of Bladders (§16.27), sea to the east, so its Cleared
Ground is x 2210 to 3160 with the Mile at the low end. `/dona` was right about what it found
and what it found was no good: the flattest place on that ground is 57 tiles (28 and 29) and
it ends at a pit 17 wide and 18 deep, x 2699 to 2715. Over the middle 560 tiles the ground
rises and falls 90 rows (y 522 to 612), with 74 steps of more than a row, the biggest of them
40, and 75 columns under water. §16.24 left the ground as vanilla made it and §16.25 looked
for the best of that. In this world there was no best to find.
*[Superseded later in session 7: 58 steps of more than a row, the biggest of them 24. The 74
and the 40 counted the shaft left in the test's input by taking a living tree's wood out of
it. See §16.29.]*

**How it was read.** §16.24 has two makes that were turned down: one that levelled the whole
Cleared Ground and one that levelled its middle half ("too flat kills it character", and of
the two ponds in the middle, "keep these too"). This ask is for the part she is met on, and it
says what flat is: undisturbed, a few holes of 5 at the deepest; natural highs and lows, but
calmer. So it is not made level. It is put on a line that follows the lie of the land, with
its highs still high and its lows still low, and the small roughness of the ground that is
already near that line is left on it. The rest of the Cleared Ground is left as it was.

**Three things changed.** `ClearedGroundPass` calms the middle of the Cleared Ground when a
world is made. The world remembers that it has such ground (`PADWorld.CalmGround`). And
`DonaQuinnEncounter` meets her at the middle of it, where it used to look for the flattest
place.
*[Superseded later in session 7: only the middle 280 tiles of her ground are made flat now,
and its edges are left to vanilla. The world remembers how much is flat as well
(`PADWorld.CalmFlat`). See §16.29.]*

### What the pass does

Between taking the floating land off and cutting the giants, `Calm`:

1. **Reads the ground as it lies**, the water on it looked through, over her ground and 70
   columns past either end.
2. **Finds its level**: the middle height of her ground.
3. **Draws the line.** The lie of the land is the ground's height averaged over 30 columns
   either way, and that averaged the same way again. The line is the level plus whatever part
   of the lie's rise and fall keeps it within 6 rows of the level.
4. **Says where each column is to stand.** Ground within a row of the line stays where it
   lies. Ground standing higher is cut to a row over the line; ground lying lower is filled to
   a row under it.
5. **Keeps a few small holes.** A run of columns lying more than a row under the line is a
   hole. It is kept, as it lay, when it was open before anything was cut, has a column of her
   ground either side of it, is 8 columns wide at the most and 5 rows deep at the most, and is
   60 columns from the last one kept; 4 at the most. Every other is filled.
6. **Walks it.** No column may stand more than a row from the next (`Layout.Level`: the low
   side comes up). A kept hole is stepped across lip to lip, as if it were not there. The lips
   come up with the rest, so each hole is measured again from the higher of its two lips:
   deeper than 5, it is filled after all; no deeper than a row, it is no hole. Then the ground
   is walked again.
7. **Eases the ends.** Past either end each column is put part of the way from where it lies
   to the height her ground has at that end: all of the way beside her ground, none of it
   where the easing stops, over 30 to 70 columns (1.6 columns for each row the two differ by).
8. **Lays it.** What stands over a column's row is taken off (`CutAbove`, which leaves a
   living tree). Soil is laid from the row down to the ground under it, and in whatever is
   hollow in the first 4 rows under it, a kept hole's bottom included: dirt, or mud where the
   column stands on mud. Water over her ground goes. The first rows of a made column are whole
   blocks, its first two rows are soil where they were stone or clay, and its wall starts at
   the third row.
9. **Takes the water that would run onto it.** Vanilla settles its liquids again after this
   pass. Water goes sideways and down and never up, so every way it could come is walked the
   other way, from the air over each column of her ground: sideways and up and never down,
   through whatever is not solid, out to 300 columns past either end. Water met on that walk
   goes. Water the ground still holds back is not met, and stays.
10. **Sods it and pours its steps.** Soil that shows gets grass, grass that no longer shows
    goes back to soil, and a made column standing a row proud of a neighbour is poured to a
    half block (`Layout.Hammer`'s rule; `WorldGen.SolidTile` is false of a half block, so none
    is turned back).
11. **Claims it** in the structure map, 40 rows over its level and 30 under, so the passes
    that ask the map before they dig leave it alone, and sets `PADWorld.CalmGround`.

*[Superseded later in session 7: steps 1 to 6 and 9 to 11 are done to the flat now, the middle
280 tiles of her ground, and not to all 560. Step 7's easing is 70 columns past either end of
the flat, inside her ground, by a share that falls smoothly, and a pond in it keeps its
water. Step 8 packs what is hollow under the flat only, and squares off the old top a fill
buries. The claim of step 11 is the flat's. See §16.29.]*

And the log's line for the pass says it: `'Dame' Quilt's ground: x 2435-2995, level at y 571,
327 columns cut down, 281 filled, small holes left: 2.`
*[Superseded later in session 7: the line gives the flat's x as well, the easing's columns
with water on them and the water taken. See §16.29.]*

**No living tree takes root on it.** `On_WorldGen.GrowLivingTree` now refuses on her ground and
115 tiles past either end of it (`Layout.NoGiantHere`: the 70 of the easing, and 45 for a
crown and roots). A giant there would stand, cut to a stump, at whatever height it grew at.
Vanilla's Living Trees pass tries another place when a tree is refused (read in its IL:
`b__288_41` rolls a new x until one grows). A giant can still root in the first and last 50
tiles of the Cleared Ground, and is still cut to a stump there.
*[Superseded later in session 7: refused within 255 tiles of the middle, which is the flat, its
easing and 45 tiles more. A giant can root in the first and last 190 tiles of the Cleared
Ground. See §16.29.]*

### The numbers

| Number | Value | Mine or theirs |
|---|---|---|
| A hole is no deeper than | 5 rows | theirs |
| A few holes: this many at the most, this far apart, no wider than | 4, 60 columns, 8 columns | mine |
| Her ground: the middle of the Cleared Ground, this far either side (`Layout.CalmHalf`) | 280 tiles: x 9605 to 10165, 560 of the 890 | mine |
| The line keeps within this of its level (`CalmRise`) | 6 rows | mine |
| The lie of the land: averaged this far either way, twice (`CalmReach`) | 30 columns | mine |
| Ground this near the line is left as it lies (`CalmRough`) | 1 row | mine |
| A step from one column to the next | 1 row at the most | the Grace plain's rule |
| Nothing hollow in its first (`Firm`) | 4 rows | mine |
| Easing past either end (`EaseLeast`, `Layout.CalmEase`, `EaseRun`) | 30 to 70 columns, 1.6 for each row | mine |
| Nothing is filled deeper than (`FillMost`) | 120 rows | mine |
| Top rows that are soil and have no wall (`WallFrom`) | 2 | mine |
| Water is looked for this far past either end (`ShedLook`) | 300 columns | mine |
| Claimed in the structure map (`ClaimOver`, `ClaimUnder`) | 40 rows over its level, 30 under | mine |
| No living tree within this of it (`CalmEase` + `Layout.GiantReach`) | 115 tiles | mine |

*[Superseded later in session 7: `EaseLeast`, `EaseRun` and `Layout.CalmEase` are gone, and the
easing is `Layout.FlatEase`, 70 columns. `Layout.CalmHalf` is still 280 and is still her
ground, but what is made flat is `Layout.FlatHalf`, 140. Nothing hollow in the first 4 rows
is asked of the flat only. No living tree within 255 tiles of the middle. See §16.29.]*

**Why 560 tiles and not the middle half.** Her box is half the Cleared Ground, 222 and a half
tiles either side of its own middle, and its middle is halfway between the player and where
she is put down (both theirs, §16.25). She comes when a player is within 1350 pixels of her
place, or 1510 by the time she is put down, and she is put down on the place or 1150 pixels
from the player. Worked through, the box's middle is within 55.2 tiles of her place, so the
box can reach 277.7 tiles from it: 280. A calm stretch of exactly half the Cleared Ground
would have the box hanging off one end of it by up to 55 tiles.
*[Superseded later in session 7: her ground is still these 560 tiles, for this reason, but only
the middle 280 of them are flat, so the box does hang off the flat now: by 82 tiles at each
end with the fight begun at her place, by 138 at one end at the most. The user asked for
hills, ponds and cracks there. See §16.29.]*

### `DonaQuinnEncounter`

- **Her place.** In a world with `PADWorld.CalmGround` it is the column nearest the middle of
  the Cleared Ground that is flat ground and has 8 columns of flat ground either side of it,
  which is to say not the bottom of a hole. In a world without, it is the flattest place, as
  before. Everything that used the flattest place uses her place.
- **Flat ground steps over a small hole**, in both kinds of world: a drop of more than a step
  that is back within a step of the lip it was stepped from inside 8 columns, and nowhere
  deeper than 5 under that lip (`FarLip`); and the same hole met from its gentle side, where
  the walk goes down by steps and meets a rise it cannot take (`OutOfHole`, which looks back
  for the lip). 4 holes at the most in one reach. The three numbers are the pass's
  (`ClearedGroundPass.HoleWide`, `HoleDeep`, `HolesMost`).
- **The band is not asked of her ground.** Flat ground used to have to keep within 6 rows of
  where it is at the column asked. Her ground rolls 12 rows by design, so on it only the steps
  are asked.
- **`/dona`** says, in a world with her ground: `Her ground: x 2435-2995, made calm. Her
  place, the middle of it: x 2715, 340 tiles of flat ground toward the Cleared Ground's start
  and 309 toward the Mile.` In a world made before, it says what it said, and then: `No
  ground was made calm for her in this world: it was made before there was any.`

*[Superseded later in session 7: `/dona` says `Her ground: x 2435-2995, made flat from x 2575
to 2855.` and, in a world made before, `No ground was made for her in this world: it was made
before there was any.` See §16.29.]*

`PADWorld.CalmGround` is the world's: cleared with the world, saved as a key that is only
written when it is set, sent to a joining client.

### In the three worlds

The pass was run on the Cleared Ground of three Grand worlds as a new world would have it
(below). Her ground is 561 columns in each.

| | The Run of Bladders | Far Tract of Potions | Life |
|---|---|---|---|
| Her ground | x 2435 to 2995 | x 9605 to 10165 | x 9605 to 10165 |
| Rise and fall before | 90 rows (y 522 to 612) | 87 (y 639 to 726) | 77 (y 647 to 724) |
| Steps of more than a row before; the biggest | 74; 40 | 64; 61 | 39; 24 |
| Columns under water before | 75 | 179 | 5 |
| Its level | y 571 | y 658 | y 668 |
| Rise and fall after | 12 rows (y 564 to 576) | 10 (y 655 to 665) | 12 (y 663 to 675) |
| Steps of more than a row after, outside a hole | 0 | 0 | 0 |
| Holes left with a step into them | 2: one column 2 deep at x 2884, five columns 5 deep at x 2774 to 2778 | 0 | 0 |
| Columns under water after | 0 | 0 | 0 |
| Columns cut down; filled | 327; 281 | 284; 316 | 306; 275 |
| Tiles taken off; put down | 5,189; 3,459 | 1,217; 3,746 | 3,864; 5,928 |
| Deepest cut; deepest fill | 43; 36 rows | 16; 64 | 18; 49 |
| Her place, and the flat ground either side of it | x 2715: 340 and 309 tiles | x 9885: 445 and 312 | x 9885: 318 and 313 |

The pass counts one hole kept in Far Tract of Potions and none shows in the table: it is a
dip with gentle sides, two or three rows down and back by steps of one, which a walker does
not meet as a hole. The pit in The Run of Bladders is gone: where it was is the middle of her
ground, and she is met on it.
*[Superseded later in session 7: these are the figures of 560 calm tiles, and the "before"
rows of The Run of Bladders and Far Tract of Potions have a living tree's shaft in them. With
280 flat tiles, and with the figures put right: §16.29.]*

### How it was checked

Nothing was run in the game.

- **The pass.** The mod's own `ClearedGroundPass.cs`, whole and unchanged, compiled against
  stand-ins for the game and run on the Cleared Ground of the three worlds, read from their
  `.wld` files as a new world would have them at that point: nothing grown yet, and no living
  tree where none would now grow. A second build of the same file with the one call to `Calm`
  left out is what it was laid beside. Read off the tiles it wrote: every column it changed
  is on her ground or its easing; no step of more than a row outside a hole; no hole wider
  than 8 or deeper than 5; no water over her ground; nothing standing or hanging over it; no
  soil showing without grass in a column it changed; four rows of ground or more under every
  top. Its wall starts two rows down in 545, 545 and 540 of the columns it changed.
- **Other country.** The same build run on 210 other stretches of the three worlds, put where
  the Cleared Ground is: every 310 columns, each world both ways round, through snow, jungle,
  desert, corruption, the dungeon's hill and the beaches. All 210 keep every one of those
  rules, with 4 holes at the most, no water left that could run onto her ground, and no tile
  read outside the stretch.
- **The water, on made-up ground**, each both ways round. A lake past the end whose rim the
  easing leaves standing keeps all 819 of its tiles. One whose rim the easing cuts loses 549
  of 1,179: what stood over the rim that is left. A cliff with a cave of water behind its
  face, low enough that the easing cuts the ground in front of it away: 6 of its 24 tiles go,
  the row the cut opened; with the step left out, one tile is left with open air beside it.
  A hole small enough to be kept, full of water, with more under a ledge at its side: with
  the step, none is left; without it, 3 tiles are left under the ledge and one of them is
  open to the hole.
- **The survey.** A Python version of the walk and of `Reach` gives, from The Run of Bladders'
  world file, the line in the user's screenshot: x 2670, 28 and 29. The mod's own compiled
  `Reach`, called by reflection out of the built `PisAndDag.dll`, gives what the Python gives
  in every one of 96,114 reaches on 87 walks: the three worlds as they are and made calm, and
  60 made-up walks with holes, trees and water in them. The hole rules change 13,088 of those.
  With them the worlds as they are keep their flattest places (x 2670 and x 2667; Life's three
  unchanged; Far Tract of Potions' x 9567 becomes x 9572 and one walk's x 9580 becomes x
  9585).
- **Her box.** Worked from the numbers in `DonaQuinnEncounter` (above): not run.
- The whole mod compiles: 0 errors, the 10 warnings it had.

**Verified in the 1.4.4 IL.** The order of vanilla's passes (`WorldGen.AddGenPasses`): Settle
Liquids is the 49th and Smooth World the 53rd, before Gem Caves, the 64th, which this pass
follows; Spreading Grass, Surface Ore and Stone, Planting Trees, Settle Liquids Again (the
97th) and Micro Biomes come after. Of Surface Ore and Stone: `WorldGen.StonePatch` turns solid
tiles into stone and hangs piles on them, and lays no block. `OrePatch` (15 to 29 of them in a
Grand world) runs a vein of ore down from the surface, a row or more down for each column it
strays, and ends it in a pocket under the ground; one step in four it lays a tile of ore up to
two columns to the side of the vein, so where the ground falls three rows in three columns
that tile can stand a row or two over it. That is vanilla's own, on her ground as anywhere,
and nothing here stops it.
`EnchantedSwordBiome.Place` asks the structure map twice: for its chamber, and for a strip one
tile wide from the surface down to it, which is its shaft. `WorldGen.PoundTile` during
generation only turns a half block on or off, after `CanPoundTile`;
`WorldGen.SolidTile(int, int, bool)` wants a block that is there, solid, no platform, no half
block, not sloped and not switched off. `b__288_41` (Living Trees) rolls a new x when
`GrowLivingTree` says no. The three cleanups that come after (Quick Cleanup, Tile Cleanup,
Final Cleanup) were not read through.

**Mine, not asked for** (all in `RELEASE_TODO.md`): every number in the table marked mine;
her ground being 560 tiles and not 445; no water left on it, which fills its ponds, and the
two in the middle were asked to be kept once; no living tree on it or near it; the ground
being sound four rows down; she is met at the middle of it and not at the flattest place;
flat ground stepping over small holes in a world made before, too; what `/dona` says.
*[Superseded later in session 7: what is made flat is 280 tiles; the ponds of her ground's
edges are kept; a giant may root on the outer 25 tiles of it at either end. See §16.29.]*

**Not in a world already made.** Her ground is made when a world is. The Run of Bladders
keeps its pit, and `/dona` there now says why. It could be added to `/struct update`; it was
not, because it would cut and fill 700 tiles of a world somebody may have built on.

**Not yet confirmed in game:** all of it. Riskiest: how the cut and the fill look where they
are deep (a hill 43 rows high is gone in The Run of Bladders, and Life's west end is 49 rows
of new soil with no stone in it); what vanilla's later passes put on it (trees, which the
cleanup turns to stumps, piles, a fallen log, once in a long while a tile of ore); Settle
Liquids Again, which the water step is reasoned about and not run against; and her fight on
it.
*[Superseded later in session 7: with 280 flat tiles the deepest cut and fill are 26 and 32
rows in The Run of Bladders, 10 and 36 in Far Tract of Potions, 14 and 29 in Life. See
§16.29.]*

**The tools are not in the mod folder.** The stand-ins, the second build, the made-up ground,
the Python survey, the program that calls the compiled `Reach`, and the pictures lived in the
session's workspace and went with it.

---

## 16.29 Later in session 7: 'Dame' Quilt's ground: flat in the middle, vanilla's at its edges

The ask, in the user's words, after the pictures of §16.28's ground:

> id also appreciate some hills , ponds, cracks or terrain generation at the edges of the
> patch but middle is flat is nice

**How it was read.** The patch is her ground as §16.28 made it: 560 calm tiles in the middle
of the Cleared Ground. Its middle is to stay flat, and its edges are to be vanilla's again:
hills, ponds, cracks, whatever vanilla generated there. Nobody was asked how much of it is
middle and how much is edge. Half and half was taken, and it is one number to change.

**What changed.** Her ground is still the same 560 tiles, because that is how far her box can
reach (§16.28). What is done to it is not the same:

| Part of her ground | Tiles from her place, the middle | In an unmirrored world | What is done |
|---|---|---|---|
| The flat | 0 to 140 either way, 281 columns | x 9745 to 10025 | everything §16.28 did to all 560: the line, the holes, sound 4 rows down, no water |
| The easing | 141 to 210 | x 9675 to 9744 and x 10026 to 10095 | each column put part of the way from where it lies to the flat's height |
| The rest of the edge | 211 to 280 | x 9605 to 9674 and x 10096 to 10165 | nothing, but for water that would run onto the flat |

### The easing

§16.28 eased past her ground, over 30 to 70 columns, into ground it had no interest in. This
one is the edge she rides out onto, and it is what turns the flat back into vanilla's ground.

- **The share.** Each column is put at `lies + (row - lies) × share`, rounded, where `row` is
  the height the flat has at that end and `share` falls smoothly from all to none over the 70
  columns: `t × t × (3 - 2t)` with `t = 1 - k / 71`, k columns past the flat. It is 0.99 at 5
  columns, 0.95 at 10, 0.81 at 20, 0.51 at 35, 0.21 at 50, 0.06 at 60 and nothing at 70. So a
  hill, a pit or a pond's bed that stands there is still there, lower, shallower and smaller
  the nearer the flat it is.
- **A pond keeps its water.** In a column of the easing that had water standing open on it,
  as many rows of it are left as `share` leaves of its bed's depth, and none of it higher
  than it stood: a pond whose water was under the height of the flat keeps its level, and
  its shore moves out. Where neither the bed nor the depth has changed, the water is not
  touched. Water that lies in the rows it is to be in is left as it lies; it is laid only
  where the bed went down from under it.
- **What is hollow under it stays hollow.** Only the flat is packed 4 rows down. §16.28
  packed its easing as well, and that was wrong for a pit with a lip: the column under the
  lip was packed down to the pit's floor while the open columns beside it were not, which
  leaves a pillar of soil one column wide standing down the pit (seen in Life at x 10082 and
  x 10085, once the pit lay in an easing).
- **But a cut that goes through into something hollow is followed.** The column lies on the
  floor of the hollow from then on and is put its part of the way from there, up to 8 times
  over. Beside the flat that fills an opened cave back in; where the easing stops it leaves
  it open, as a pit that lay open already is left. Without this the rim of the flat could be
  a chasm: on other stretches of the three worlds, a cut in the first column past the flat
  dropped it 72 rows into a cave.
- **No step rule.** Past the flat a column may stand any number of rows from the next, as
  vanilla's does. The steps of one row that are left on made columns are still poured to
  half blocks.

### The rest of what changed

- **The fill squares off what it buries.** The ground a fill comes to rest on was a top until
  then, and vanilla's Smooth World may have sloped it or poured it to half. `Fill` makes it a
  whole block again. §16.28's did not, and left a gap under the fill wherever the old top was
  shaped: 40, 80 and 53 of them in the three worlds as the pass now runs. That was a fault in
  what was delivered, not something asked for now. A world made with §16.28's build has them.
- **The world remembers how much is flat.** `PADWorld.CalmFlat`: the tiles either side of the
  middle that were made flat, set by the pass beside `CalmGround`, saved (only with
  `CalmGround`), sent to a joining client. A world made with §16.28's build has the first key
  and not the second, and is read as flat from end to end (`Layout.CalmHalf`). `/dona` says
  what the world says and not what the mod's number is today, because that number has now
  changed once.
- **Giants.** `Layout.NoGiantHere` now covers the flat, the easing and 45 tiles more: 255
  tiles either side of the middle, where it was 395. A giant can root in the first and last
  190 tiles of the Cleared Ground, the outer 25 of her ground at each end among them, and is
  cut to a stump there.
- **The claim.** The structure map is told of the flat only: 281 columns, 40 rows over its
  level and 30 under. The edges are vanilla's to dig in.
- **The water that would run onto the flat** is looked for as before, from the air over each
  column of the flat, out to 300 columns past either end. That now reaches through the edges
  and past her ground. A pond there that nothing lowered the rim of is not met.
- **The log.** `'Dame' Quilt's ground: x 2435-2995, flat from x 2575 to 2855 and level at y
  575. 241 columns cut down, 159 filled, small holes left on the flat: 0, columns with water
  on them where the flat eases: 6, tiles of water taken that would have run onto the flat: 2.`
- **`/dona`.** `Her ground: x 2435-2995, made flat from x 2575 to 2855. Her place, the middle
  of it: x 2715, 157 tiles of flat ground toward the Cleared Ground's start and 197 toward the
  Mile.` In a world made before there was any: `No ground was made for her in this world: it
  was made before there was any.` The two reaches are the encounter's flat ground, which
  steps 2 rows at the most, and run on past the made flat for as long as vanilla's ground
  steps no more than that.

Nothing in her fight or in where she is met was changed. She is still met at the middle, which
is on the flat.

### The numbers

| Number | Value | Mine or theirs |
|---|---|---|
| The middle flat, the edges vanilla's | | theirs |
| The flat: this far either side of the middle (`Layout.FlatHalf`) | 140 tiles, half of her ground | mine |
| The easing past either end of it (`Layout.FlatEase`) | 70 columns | mine |
| The rest of each edge, not touched | 70 columns | mine: what the two leave |
| How the share falls | smoothly, `t²(3 - 2t)` | mine |
| A pond in the easing keeps | `1 - share` of its depth, and none higher than it stood | mine |
| A cut is followed into what is hollow (`Falls`) | 8 times at the most | mine |
| No living tree within this of the middle (`FlatHalf + FlatEase + Layout.GiantReach`) | 255 tiles | mine |
| Every number of the flat itself | as §16.28 | as §16.28 |

### In the three worlds

The pass run on the Cleared Ground of the three Grand worlds as a new world would have it.

| | The Run of Bladders | Far Tract of Potions | Life |
|---|---|---|---|
| Her ground | x 2435 to 2995 | x 9605 to 10165 | x 9605 to 10165 |
| The flat | x 2575 to 2855 | x 9745 to 10025 | x 9745 to 10025 |
| Its level | y 575 | y 656 | y 668 |
| The flat's rise and fall before | 68 rows (y 544 to 612) | 54 (y 645 to 699) | 38 (y 649 to 687) |
| after | 12 rows (y 568 to 580) | 9 (y 654 to 663) | 13 (y 662 to 675) |
| Steps of more than a row on it before; the biggest | 33; 24 | 19; 5 | 6; 15 |
| after | 0 | 0 | 0 |
| Its columns under water before; after | 0; 0 | 108; 0 | 2; 0 |
| Holes kept on it | 0 | 0 | 0 |
| Columns cut down; filled | 241; 159 | 186; 183 | 169; 148 |
| Tiles taken off; put down | 2,786; 1,858 | 583; 2,302 | 1,188; 1,285 |
| Deepest cut; deepest fill | 26; 32 rows | 10; 36 | 14; 29 |
| Columns changed, of the 421 | 396 | 371 | 315 |
| Water standing in the easings before; after | 9 tiles; 9 | 572; 66 | 5; 4 |
| Water in the rest of the edges | 679 tiles, all kept | 362, all kept | none |
| Water taken as about to run onto the flat | 2 tiles | 8 | 0 |
| Her place, and the flat ground either side of it | x 2715: 157 and 197 tiles | x 9885: 146 and 200 | x 9885: 190 and 150 |

The two reaches depend a little on where the walk is started from, as they always have: from
four of the eleven places tried in The Run of Bladders the first is 171, and from three in Far
Tract of Potions the second is 176.

What that is, world by world. **The Run of Bladders:** the hill toward the Mile is back, its
top at y 522 where the easing stops, 46 rows over the flat and 70 columns from it, with the
crack of water in its side; toward the start the lake is whole, 56 columns of it and 27 rows
deep, with the ground rising 11 rows from the flat to its rim. §16.28 had cut the hill off and
filled the lake in. **Far Tract of Potions:** the pond that lies across the start end of the
flat is filled where it is on the flat, and is a pool 31 columns wide and 3 deep past it; the
pond in the rest of the Mile edge is whole, 38 columns and 19 deep. **Life:** the valley
toward the start is a slope of 48 rows down from the flat; toward the Mile the crack of water
11 columns past the flat is open to the sky again, narrower, and the pit 56 columns past it
keeps its shape and its lip.

### How it was checked

Nothing was run in the game.

- **The pass**, as §16.28: the mod's own `ClearedGroundPass.cs`, whole and unchanged, compiled
  against stand-ins and run on the three worlds' Cleared Grounds, beside a second build with
  the one call to `Calm` left out. Read off the tiles it wrote. On the flat: every rule of
  §16.28. In the easing: every one of its 140 columns stands exactly where the rule above
  puts it, worked a second time in Python from the tiles the pass was given; no water in a
  tile that was open sky before; no more water in a tile than it held; none higher than any
  stood in its column. Past the easing: not a tile, a wall or a shape changed, and no water
  added. No sloped or half block left shut in under the ground. No soil showing without
  grass in a column it changed.
- **The water, forward.** The pass looks for water that could reach the flat by walking back
  from the flat, sideways and up. The check goes the other way: from every tile of water
  within 300 columns of the flat, sideways and down through whatever is not solid. It reaches
  the air over no column of the flat, in any run here; nor does it from 410 columns out.
- **Other country.** 204 other stretches of the three worlds, every 310 columns, each world
  both ways round, with nothing grown on them. 199 keep every one of those rules. The other 5
  are all in Life and all lie over its Wasted Mile, where the mod's own bones stand on ash
  poured to half: a half block with a block on it is no top, the fill beside it shuts it in,
  and the check counts it (1 to 15 of them). Nothing of the kind is on the Cleared Ground
  when the pass runs. Over the 204: water was taken as about to run onto the flat in 45, 367
  tiles at the most; in 13 some of it came from past the easing, 270 tiles at the most; of
  the 408 easings 143 had water standing on them before and 93 after, 23,275 tiles before and
  7,407 after; a cut went through into a hollow in 626 columns of 81 stretches, and within 5
  columns of the flat that left the ground a row low at the most, within 10 columns 3 rows,
  within 20 columns 7; holes kept on the flat, 38 in all and 2 at the most.
- **The water, on made-up ground**, 12 grounds, each both ways round, each run without her
  ground, with it, and with it but without the step that takes water. All 24 keep the rules.
  A pond in the rest of the edge: all 401 of its tiles kept. A pond in the easing with its
  water under the height of the flat: 203 of 401 left, its level where it was. The same pond
  brim-full: 47 tiles taken from its top, 255 of 517 left. A pond across the end of the flat:
  85 of 1,044 left, all past the flat. A pond on the flat: none of its 739 left, and its bed
  filled. A lake across a whole edge and on past it: 5,213 of 5,640. A pond on high ground in the easing: 70 of 291. A lake on high ground past the
  easing, its rim in the easing: all 665. The same lake reaching into the easing: 607 of 951,
  what stood over the rim that is left. A lake on high ground whose rim was on the flat: 81
  of 1,150, a row of water on its bed. A small hole on the flat, full, with water under a
  ledge beside it: kept, and dry. A cave of water behind a cliff's face that the easing cuts
  open: 12 of 24. Without the step that takes water, water could reach the flat in 7 of the
  12.
- **The survey.** The Python walk and `Reach` on the three worlds with her ground made, from
  11 places a player could stand in each: her place is the middle from every one of them.
  The mod's compiled `Reach`, called out of the built `PisAndDag.dll`, gives what the Python
  gives in all 117,612 reaches of the 66 walks.
- The whole mod compiles: 0 errors, the 10 warnings it had.

**The test worlds are played worlds, and two things in them were wrong for this.** Far Tract
of Potions has a fishing bridge of the player's over the pond on her ground: platforms, a
campfire and six bubble blocks. The bubbles are solid, and stood as ground over the water.
They are taken out of the input now, with everything else that is not there when the pass
runs.

And two of the worlds have a living tree where a new world now grows none: The Run of
Bladders at x 2835 to 2842, Far Tract of Potions at x 9652 to 9662 with a room off it. §16.28
took the wood, the leaves and the heartwood out of the input and left what was dug for the
tree: the shaft its trunk was sunk in, about 43 rows deep in one world and 56 in the other.
No new world has that shaft, because no tree is planted. The input now has the ground put
back as near as can be told: in the columns the hollow runs through, soil from the ground
line down, the line drawn straight between the ground either side; a root that rests on
ground is soil, one with air under it is air.

**That shaft was in §16.28's figures.** Its side was "the biggest step" of the table there: 40
rows in The Run of Bladders and 61 in Far Tract of Potions. With the ground put back, her 561
columns were, before anything was done to them: The Run of Bladders, 90 rows of rise and fall
as it said, 58 steps of more than a row and not 74, the biggest 24 and not 40; Far Tract of
Potions, 60 rows and not 87, 53 steps and not 64, the biggest 22 and not 61, 185 columns
under water and not 179; Life, as it said. What §16.28 says of those two worlds after the
pass (columns cut and filled, tiles moved, the deepest fill) has the shaft in it too, and was
not worked again. The complaint the work answered does not rest on any of that: the pit
beside `/dona`'s place, 17 wide and 18 deep, is measured in the world as it is.

**Verified in the 1.4.4 IL.** `Liquid.Update`, `SettleWaterAt`, `QuickWater` and
`UpdateLiquid` ask nothing of a half block or a slope: liquid is stopped by a tile that is
there, not switched off, solid and no platform, whatever its shape (`tilesIgnoreWater` turns
`tileSolid` off for three tiles only: 138, 484 and 546). That is the rule the water step
walks by. `WorldGen.do_worldGenCallBack` calls `clearWorld`, then `GenerateWorld`, then
`WorldFile.SaveWorld`, and `clearWorld` is what calls `SystemLoader.ClearWorld`: so what a
pass sets in `PADWorld` is cleared before generation and saved with the new world.

**What it costs.**

- **Her box reaches the edges.** It is 445 tiles and the flat is 281. With the fight begun at
  her place, 82 tiles at each end of the box are edge: the easing, and 12 tiles of ground
  nothing touched. With the box as far off her place as it can fall, 138 at one end. Hills,
  water and pits deeper than 5 can be inside the box again, at its ends. That is the ask; it
  is also where §16.28's complaint came from. Triunfo jumps 10, leaps 14 and swims.
- **A pond can lose water.** One whose rim the flat or the easing brings down under it keeps
  what stands under the rim that is left. One whose rim was on the flat keeps a row.

**Mine, not asked for** (all in `RELEASE_TODO.md`): every number in the table marked mine;
ponds in the easing keeping their water, scaled; hollows left under the easing, and a cut
followed into one; giants let back onto the outer 25 tiles of her ground; the claim shrunk to
the flat; `PADWorld.CalmFlat`; the two counts added to the log's line; what `/dona` says; the
fill squaring off what it buries.

**Not in a world already made**, as before. A world made with §16.28's build keeps its 560
calm tiles, and `/dona` there says `made flat from x 2435 to 2995`.

**Not yet confirmed in game:** all of it. Riskiest: how the easing looks where the flat and
the edge are far apart in height (46 rows in 70 columns on The Run of Bladders' hill); what a
shallow pool beside the flat looks like once vanilla has settled it; her fight where it
leaves the flat.

**The tools are not in the mod folder.** The stand-ins, the second and third builds, the
checks, the made-up grounds, the Python survey and the pictures lived in the session's
workspace and went with it.

---

## 16.30 Later in session 7: the parry made universal: every enemy classed, the stagger and the riposte, solid shots, 'Dame' Quilt parried

The ask, in the user's words, after the classes of enemy a parry could reach had been put to
them:

> Alright then its settle, parrying these enemies will deal like base damage to them and if
> flag staggered, their AI will be changed to staggered(must remember their normal AI tho so
> they can resume after sometime(check how long can an NPC stagger for in elden ring)), parry
> wont deal damage anymore but any attack that isnt parry gets triple damage and quad knock
> back(the attack wont have crit)
> Also : Solid things shot or thrown (arrows, javelins, knives, bones) (maybe bullets too but
> only the penetrate 1 type)

Four things were asked back. The answers:

| Asked | Answered |
|---|---|
| The riposte: the first hit, or every hit while it is staggered | "First hit onnly and it immediately wakes it" |
| How long the stagger is | "2 seconds (Recommended)" |
| Giants | "Cannot be parried but primary hit still do damage" |
| Bullets | "All bullets for vanilla IG? but other mods penetrate 1" |

Then, while it was being written:

> Also now player can also parry Dame Quilt, all her attacks(joust, thrown lance and horse
> charge) also all parry has to push NPCs back btw

> make it cool looking tho

**How these were read.**

- *These enemies*: an enemy is classed at the moment of the parry, from its own numbers, as
  never, giant, heavy or light (below). No list of what can be parried: only short lists of
  what cannot be told from the numbers.
- *Deal like base damage*: the parry's blow. The damage of the weapon that parried, as the
  player has it (`Player.GetWeaponDamage`), less the enemy's defence as any hit is; never a
  critical hit, none of the game's variation, no knockback of its own.
- *Their AI will be changed to staggered, must remember their normal AI*: the AI is stopped,
  not swapped. Nothing of the enemy's is changed (`aiStyle`, `ai`, `localAI`, its own fields),
  so there is nothing to put back: it goes on from where it stopped.
- *Parry wont deal damage anymore, but any attack that isnt parry gets triple damage and quad
  knock back, the attack wont have crit*: a staggered enemy cannot be parried again. The first
  hit on it that is not a parry does three times its damage and four times its knockback, is
  never a critical hit, and wakes it at once (the answer above).
- *Check how long in Elden Ring*: nothing read gives a figure (below). Two seconds, as
  answered.
- *Giants cannot be parried but primary hit still do damage*: read as: a giant's hit is not
  turned aside and lands on the player; it is not pushed and never staggered; the parry's own
  blow (the primary hit) still lands on it, once a parry. **This reading is mine**: say if it
  meant something else.
- *Bullets*: vanilla's enemies have three (the pirate's, the snowman's, the sniper's) and all
  three are parried. Any other bullet only when its `penetrate` is 1.
- *All parry has to push NPCs back*: every enemy a parry turns aside is pushed back 96 pixels,
  whether or not the game lets it be knocked back. A giant is not turned aside, so not pushed.
- *All her attacks (joust, thrown lance and horse charge)*: the charge with the lance couched,
  the lance thrown, and Triunfo running them down. Her fourth, the swing, is not among the
  three and wears the yellow star: it is not parried.

**Elden Ring, as asked.** No wiki or guide that was read gives the length of the stagger.
eldenring.wiki.gg, Stance: an enemy whose stance is broken is "stunned for a significant
amount of time", and a riposte knocks it back, "resetting their Poise and taking them out of
the stun" (so the first hit ends it there too). The same site, Parrying: "bosses often require
two or even three parries before becoming vulnerable to a Critical Hit (note that there's no
recovery for this ...)". eldenring.wiki.fextralife.com, Parrying: "For most parriable enemies,
a single successful parry is enough", and "an enemy's parry counter doesn't decay after a
period of inaction". darksouls.fandom.com, Parry and Riposte, for the older game: "players
have about one second to do the Riposte". What was taken from these: one parry for most, two
for the ones that do not give; a count that does not run down; the first hit ends it.

**What was made.**

| Piece | Where | What it does |
|---|---|---|
| `ParryRules`, `ParryClass`, `ParryWhy` | `Content/Combat/ParryRules.cs` (new) | What can be parried, read off the thing itself: an enemy's class and why, and whether a shot is solid. Nothing of the mod's state in it, so it runs outside the game. |
| `Parry` (static) | `Content/Combat/Parry.cs` (rewritten) | The lists (the mod's, the config's, other mods'), what a parry does to a thing, the packet. |
| `ParryPlayer` | same file | A player's side: the ticks, the arc, vanilla's two questions, the blow, and the four doors for hits the mod works out itself (`Spares`, `MeetsLine`, `MeetsBox`, `Takes`). |
| `ParrySystem` | same file | Once everything is loaded: finds what every ammunition shoots, reads the config, writes one line of counts to the log. Lets go of all of it on unload. |
| `PADStaggerNPC` | `Content/NPCs/PADStaggerNPC.cs` (the Greatbow's stagger kept as it was; the parry's added) | The stagger a parry gives (`Reel`), the push (`Push`), the count toward a stagger (`TakeParry`), the riposte, what is sent. |
| `ParryEffects` | `Content/Combat/ParryEffects.cs` (new) | Everything the parry shows and sounds. |
| `ParryRing` | `Content/Particles/ParryRing.cs` (new) | A ring of light: the roar's ring (`Content/Combat/RoarRing.png`, already in the mod), drawn twice a third of a turn apart. No new picture. |
| `ParryConfig` | `Content/Combat/ParryConfig.cs` (new) | Five lists for whoever runs the world. The mod's first config. |
| `ParryCommand` | `ParryCommand.cs` (new) | `/parry`: what a parry makes of the enemy nearest the cursor, and why. `/parry list`: every kind, to the log. |
| `DonaQuinn` | `Content/NPCs/DonaQuinn/DonaQuinn.cs`, `DonaQuinn.Fight.cs` | A new phase, `Reel`; her charge and her ram ask the parry before they land. |
| `DonaLanceThrown` | `Content/NPCs/DonaQuinn/DonaLanceThrown.cs` | Parried in the air, it is knocked away turning end over end. |
| `AbomanationBone`, `AbomanationSkull` | `Content/NPCs/Abomanation_Small/AbomanationSpew.cs` | Said to be solid, as the bones they are. |
| `IronFlowHoldout`, `IronFlow` | their two files | The sword gives itself as what parries: `ParryPlayer.Begin(timing, facing, weapon)`. |
| `Mod.Call` | `PisAndDag.cs` | Five words for another mod. |
| the words | `Localization/en-US_Mods.PisAndDag.hjson` | The Iron Flow's lines about the parry, and `Configs.ParryConfig`. |

**An enemy's class** (`ParryRules.Kind`, asked in this order; the first that fits is the answer).

| Class | What it is | A parry |
|---|---|---|
| Never | friendly, a town NPC, or no damage by touch; a shot in an enemy's place (`NPCID.Sets.ProjectileNPC`, and the Ancient Light and the Ancient Doom, which lack the flag); a boss (`NPC.boss` or `NPCID.Sets.ShouldBeCountedAsBoss`); a part of a boss (25 named below, or `realLife` pointing at one); beyond being hurt (`dontTakeDamage`, `immortal`); a bomb (8 named); a ghost (`aiStyle` 22 that also goes through tiles) | does nothing |
| Giant | a hitbox wider than 60 or taller than 84 (three players wide, two tall) | lands its blow, once; the giant's hit lands too |
| Light | the game lets it be knocked back (`knockBackResist` above 0, as it is at that moment) | turns it aside, strikes it, pushes it back, staggers it |
| Heavy | the game does not (`knockBackResist` 0) | turns it aside, strikes it, pushes it back; the second parry of it staggers it |
*[Superseded later in session 7: light and heavy are staggered alike now: every second parry of
one that flies, every third of one that does not. See §16.31.]*

Over that, in `Parry.Of`: an enemy that is staggered already is never (a parry is no riposte);
one that wears the yellow star (`PAD().CCBypass`) is never; one on a "never" list is never; one
on an "always" list, or one the mod speaks for (`Parry.AddNPC`), has its size and its rank set
aside and is light or heavy by its weight alone.

`knockBackResist` is the line because the game draws it there itself:
`NPC.GetIncomingStrikeModifiers` switches knockback off for an enemy at exactly 0. It is read
live because nine of vanilla's AIs move it while the enemy lives (the Creature from the Deep
is 0 in water and 0.4 on land). Life is not looked at: it does not sort them (a Giant Worm's
segment has 30 and cannot be pushed, a Nailhead has 4000 and can), and the world's difficulty
doubles and triples it, where it only takes a tenth or a fifth off `knockBackResist` and
leaves 0 at 0.

The 25 parts of bosses, by name: the Eater of Worlds' body and tail, Skeletron's hand, The
Hungry, Prime's four arms, the Destroyer's body and tail, Golem's two fists, Plantera's hook
and tentacle, the Martian Saucer's turret and cannon, the Moon Lord's free eye, the Flying
Dutchman's cannon, the Phantasm Dragon's six parts, the Ancient Vision. The 8 bombs: the
Chattering Teeth Bomb, the Kobold and the Kobold Glider (both tiers), the Etherian Goblin
Bomber (three tiers).
*[Superseded later in session 7: the Golem's two fists are parried in their punch. The rules still list them; the mod speaks for them itself, and it is asked first. See §16.33.]*

**Vanilla, by those rules**, run on the compiled mod against the real assembly's own samples,
all 687 types: 514 are hostile and hurt by touch. 353 light. 59 heavy, of which 24 can be
staggered (among them the Paladin, the Yeti, the Pirate Captain, the Man Eater, the crawling
Lihzahrd) and 35 never, being worms or their segments. 25 giants (among them the Ice Golem,
the Mourning Wood, the Pumpking, the Everscream, the Ice Queen, Santa-NK1, the Ogre, Betsy,
Mothron, the Dungeon Guardian, the sharks). 77 never: 25 parts of bosses, 19 bosses, 13 shots,
8 bombs, 7 beyond being hurt, 5 ghosts (the Wraith, the Ghost, the Reaper, the Poltergeist,
the Floaty Gross).
*[Superseded later in session 7: in the game the Golem's two fists are heavy and never staggered, so 61 heavy (24 staggered, 35 worms, the 2 fists) and 75 never (23 parts of bosses). The rules' own table is as above. See §16.33.]*

**What a parry does to an enemy.** In this order, on the machine of the player who parried:

1. Its hit is turned aside, and it cannot touch that player for 45 ticks.
2. The blow: `Player.ApplyDamageToNPC(npc, Player.GetWeaponDamage(weapon), 0, direction,
   crit: false, weapon.DamageType, damageVariation: false)`. A hit of the player's like any
   other: the game sends it, and everything that answers a hit answers it. Before the stagger,
   so it is never taken for a riposte. None with no weapon given, or on something nothing can
   hurt.
3. Then, where the enemy is simulated (`Parry.Throw`): it is pushed back and, if it is to be,
   staggered.

**The push** (`PADStaggerNPC.Push`). 96 pixels, written into its position like the Greatbow's
slide, so nothing resists it, but as a shove that dies away: 24 ticks, each step nine tenths
of the last, the first 10.4 pixels. Along the way from the player to the enemy; along the
ground for anything that stands on it. Stopped by tiles (`Collision.TileCollision`) unless the
enemy goes through them anyway. What speed it had toward the player is taken off it first:
left on, a charge would carry it most of the way back through the push.

**The stagger** (`PADStaggerNPC.Reel`). 120 ticks. Light: at the first parry. Heavy: at the
second (`Parry.HeavyParries`); the count does not run down, and starts over at each stagger.
Never: a worm or any segment of one (`aiStyle` 6), any part of something many-parted
(`realLife`), anything on the Greatbow's own list, anything a list says. For those ticks:
*[Superseded later in session 7: at every second parry of an enemy that flies and every third
of one that does not, whatever its weight (`Parry.FlyingParries`, `GroundParries`). The rest
stands. See §16.31.]*

- Its own AI is not run (`PreAI` says no), and in its place it slumps: it loses 14% of its
  speed across the ground each tick. What has weight falls as the game makes it. What flies
  falls at the game's own rate and lies where it lands, unless it also goes through tiles,
  when it hangs. What has weight and goes through tiles is held up (left to the game it would
  drop out of the world).
- It can hit nobody (`CanHitPlayer`, `CanHitNPC`).
- Three glints go round over its head, and its body beats toward steel twice a second.
- **The first hit on it is the riposte** (`ModifyIncomingHit`): `FinalDamage` times 3, so
  after its defence; `Knockback` times 4; `DisableCrit()`. Whoever's it is and whatever it is:
  a swing, a shot, a minion, a trap. A hit on one part of a many-part thing is a hit on the
  whole. **It wakes at once** (`HitEffect`): its AI runs again on the next tick, with the
  knockback on it. A burn or a poison is not a hit and does not wake it.
- On an enemy the game does not let be knocked back, four times nothing is nothing: the
  riposte's knockback is the game's, and the game gives those none.

A flying weapon is not stopped: it is put in its AI's own state for a hit and thrown back at 8
pixels a tick, as before, and is open to the riposte while it spins (`Reel` with `hold`
false).
*[Superseded later in session 7: on every second parry of it. The first strikes it and pushes
it back like anything else. See §16.31.]*

**An enemy has to be attacking.** At the arc: able to hit this player now (what vanilla asks:
not friendly, damage, not one the game leaves this player alone by, a touch that can be dodged,
its own and every mod's `CanHitPlayer`), and coming at them (its speed toward them). On the
player's body: moving at all, vanilla's own rule for its shield parry. One walked into is not
parried. A flying weapon is the exception, as it was.

**Shots** (`ParryRules.Solid`). Hostile, with damage, without the yellow star, and solid.

- Vanilla's are named: 26 of its enemies' own (the two arrows, the three javelins, the
  skeleton's bone, the Rock Golem's rock, the snowball, the Harpy's feather, the two stingers,
  the nail, the pine needle, the ornament's shrapnel, the frost shard, the five spikes, the
  three seeds, the three darts) and 8 of the player's thrown things, for another mod's enemy
  that throws them. With the three bullets: 29 hostile kinds, of vanilla's 164 that are
  hostile by default. No light, fire, magic or spit.
- Anything with vanilla's `arrow` flag. Anything some arrow or dart in the game shoots (found
  at load from every item that is ammunition, vanilla's and every mod's). A mod's shot that
  says it flies as one of these (`ModProjectile.AIType`).
- Any other bullet (what a bullet in the game shoots, or a mod's that flies as one) only when
  its `penetrate` is 1. So a Musket Ball in an enemy's hands is parried, and a Meteor Shot, a
  High Velocity Bullet and a Luminite Bullet are not.
- Vanilla's own enemies' bullets are named instead, because the rule cannot be used on them:
  their `penetrate` is -1, and the game never looks at it when a hostile shot hits a player.

A parried shot is destroyed, unless the mod says what else becomes of it
(`Parry.AddProjectile(type, when, turned)`).

**'Dame' Quilt.** She is a boss, and the mod speaks for her (`Parry.AddNPC` with a `when` and
a `thrown`): she can be turned aside while she charges, while Triunfo runs them down, and
while she pulls up from either (`CanBeTurned`). Her hits are worked out by the mod, not by the
game's touch, so each asks the parry before it lands: the lance its line
(`ParryPlayer.MeetsLine`), Triunfo his body (`MeetsBox`), and both are held off for the 45
ticks after (`Spares`).

- She is heavy (`knockBackResist` 0). Every parry: the blow on her, and a new phase, `Reel`:
  Triunfo thrown back the way he came (5.9 pixels a tick and a hop, pulling up at his own
  rate: about 94 pixels, six tiles), her lance knocked up out of line, 40 ticks, then she
  faces them and picks again.
- Every second parry staggers her: `Reel` for 120 ticks, Triunfo up on his hind legs for the
  first 30, then she slumps over his neck with the lance drooping. She has her own state for
  it, so her AI is not stopped: only open. The first hit on her or on Triunfo (his life is
  hers) is the riposte, and she gathers herself at once.
  *[Superseded later in session 7: every third parry, as for anything on the ground. See
  §16.31.]*
- The thrown lance, met in the air: knocked up and back the way it came (5 across, 9.5 up),
  turning over three times about its own middle in 30 ticks, hurting nobody, throwing sparks
  for the first 18. It comes down about 270 pixels off and goes into the ground there like any
  throw; she rides back to wherever that is. No blow on her: she is not there.
- The swing is not parried.

**Amalgaermis.** Its bones and its skulls are thrown bones, so they are said to be solid
(`Parry.AddProjectile` in each). A skull broken by a parry spills its three bones where it was
met, as it does wherever it breaks; they are bones too. Its hands and its ash are not solid.

**The doors, for what the rules get wrong about another mod's things.**

- The config (`ParryConfig`, the server's): enemies never parried; enemies always parried;
  enemies never staggered; shots a parry breaks; shots never parried. A "never" list wins.
- `Mod.Call("ParryNPC" | "ParryNoNPC" | "ParryNoStagger" | "ParryProjectile" |
  "ParryNoProjectile", type)`: the same five, from code. True when taken, false when the type
  is no number; any other word is passed on to `FireAffinity.Call`.
- `/parry` says what a parry makes of the enemy nearest the cursor as it is that moment, and
  why. `/parry list` writes every kind of enemy that hurts by touch, and every solid shot, to
  the log.
- At load, one line in the log: `Parry: of N kinds of enemy that hurt by touch, ...`.
- In code: `Parry.AddNPC(type, when, thrown)` and `Parry.AddProjectile(type, when, turned)`,
  as before; `thrown` is now given who parried, the way from them to it, and whether this
  parry staggers it.

**How it looks** (`ParryEffects`; white and steel, never the warning yellow; nothing drawn
from a picture of its own).

| When | What |
|---|---|
| A shot turned aside | as before: a star, a bloom, nine shards that fall, `Item37` and `NPCHit4` pitched up |
| An enemy turned aside | that, a ring opening from 6 to 62 pixels in 16 ticks, and eight shards thrown the way it is sent |
| Pushed back | on the ground, the ground's own dust off its feet (`GroundWave.FogColour`, `DustFog`); in the air, pale streaks left behind it |
| Staggered | a ring off the whole of it, a star over its head, eight shards off the top of it, `NPCHit4` pitched down and `DD2_MonkStaffGroundImpact` |
| While it is | three glints going round over its head on a flat ring, one lit every 3 ticks; its body beating toward steel |
| The riposte | a cut of light through it the way it is thrown (`SlashStreak`), two rings, a star, a bloom, fourteen shards that way, `Item71` and `DD2_MonkStaffGroundImpact` |
| The riposte, for the one who lands it | the camera leans in (1.05 for 7 ticks), a shake of 6, a breath of light |
| 'Dame' Quilt turned aside | Triunfo's hooves take the dirt with them; her head thrown back |
| Her lance knocked away | it turns end over end, shedding sparks |

A storyboard of the first five and the riposte was drawn outside the game from the mod's own
textures and sums, and shown in chat.

**Who decides.** As before: the machine of the player who parried. With a server, that
machine lands the blow (the game sends it as it sends any hit), shows the clash, and sends the
same 13 bytes; the server looks at the same things as before, does the push and the stagger,
and marks the enemy to be sent. The stagger, whether it is held, and the push go with the
enemy (`SendExtraAI`: four bits, and up to 13 bytes). The riposte is worked out where the hit
is, like any hit; it wakes the enemy there at once, and a word from the server that left
before the riposte reached it is not believed for 20 ticks. On the machine that parried, the
enemy is not turned until the server says so: the 45 ticks cover that.

**The numbers.**

| Number | Value | Mine or theirs |
|---|---|---|
| The blow | the parrying weapon's damage | theirs ("like base damage"); no crit, no variation and no knockback are mine |
| The riposte | 3 times the damage, 4 times the knockback, no crit | theirs |
| It is the first hit only, and wakes it | | theirs |
| The stagger | 120 ticks | theirs |
| Giants: the hit lands, the blow lands | | theirs, as read |
| A giant is | wider than 60 or taller than 84 | mine |
| Light or heavy | `knockBackResist` above 0, or 0 | mine |
| A heavy enemy is staggered on | the 2nd parry; the count never runs down | mine (Elden Ring's way) |
| Every parry pushes back | | theirs |
| The push | 96 px, over 24 ticks, each step 0.9 of the last | mine |
| Solid: arrows, javelins, knives, bones | | theirs; which of vanilla's shots those are is mine |
| Bullets | vanilla's three; others at `penetrate` 1 | theirs |
| 'Dame' Quilt: the charge, the thrown lance, Triunfo's charge | | theirs |
| She is staggered on | every second parry | mine (she is heavy) |
| Turned aside, she is out of it for | 40 ticks | mine |
| Triunfo is thrown back at | 5.9 px a tick, a hop of 3 | mine |
| Her lance is knocked away at | 5 across, 9.5 up; three turns in 30 ticks | mine |
| The camera for a riposte | 1.05 for 7 ticks, shake 6, flash 0.06 | mine |
| A staggered thing loses of its speed | 14% a tick | mine |
*[Superseded later in session 7: staggered on every second parry (flying) or every third (the
rest), theirs; 'Dame' Quilt on every third. See §16.31.]*

**How it was checked.** Nothing was run in the game.

- `Parry.cs`, `ParryRules.cs`, `ParryTiming.cs`, `ParryConfig.cs` and `PADStaggerNPC.cs`, whole
  and unchanged, compiled against stand-ins and run tick by tick in the game's own order
  (below): 224 checks, all passing. A Zombie parried: one blow of 27 (30 less half its 6
  defence), not tripled, no crit; its AI not run that tick or for 120 ticks; pushed 10.4
  pixels on the first tick and 96.0 in 24; unable to hit; then its AI goes on with its own
  count one more than it was. The riposte: 50 on it does 141, no crit though one was rolled,
  knockback 10 for 5, awake at once, and the next hit is an ordinary 47. A bullet as the
  riposte; somebody else's; a burn not waking it. A Paladin: the first parry strikes and
  pushes it and its AI goes on; the second, 600 ticks later, staggers it; a riposte of 200
  does 525 with no knockback; then the count starts over. An Ice Golem: one blow a parry, its
  hit lands. A boss, a ball of fire, a boss's hand, a ghost, a bomb, one that cannot be hurt,
  one under the star, a friend, a part of another mod's boss: nothing. One standing still,
  one going away, one from behind, too early, too late: not parried. A worm and a segment:
  parried, pushed, never staggered. A flyer: pushed up and out, falls, lies, gets up. A
  flying weapon: as it was, and open. Shots of every kind above. A lance, a body and a thrown
  thing through the mod's own doors, on a boss the mod speaks for: not staggered, then
  staggered; spared 45 ticks to the tick. A wall behind the enemy; one that goes through
  walls; one with weight that goes through tiles. The config's lists and another mod's words.
  Two machines: the client, the server (and what it will not believe), another client, the
  bytes sent with the enemy read whole, the late word.
- The rules on every vanilla type, with the real assembly: the compiled `ParryRules.Kind` on
  all 687 samples (the counts above), and `ParryRules.Solid` on all 1021 projectiles.
- 'Dame' Quilt's own code was not run: read, line by line, against the phases beside it.
- The whole mod compiles against the real 1.4.4 assemblies: 0 errors, 10 warnings.

**Read in the 1.4.4 assembly's IL for this.** `Main.DoUpdateInWorld`: every player, then
every NPC, then every projectile. `Player.Update`: `PlayerLoader.PreUpdate`,
`Update_NPCCollision`, the move, `ItemCheckWrapped`, `PlayerLoader.PostUpdate`.
`Update_NPCCollision` asks `CombinedHooks.CanNPCHitPlayer` (`NPCLoader.CanHitPlayer`: every
global, stopping at the first no, then the NPC's own; and only then
`PlayerLoader.CanBeHitByNPC`) before it looks at whether the player is immune, at
`npcTypeNoAggro`, or at the rectangles. `NPC.UpdateNPC_Inner`: the gravity is worked out
(`UpdateNPC_UpdateGravity`), then `AI`, then `velocity.Y += gravity` for what has weight, then
`UpdateCollision` (or the bare move, for what goes through tiles), and `justHit` and
`netUpdate` are cleared at the very end. `NPCLoader.NPCAI`: every global's `PreAI`, the AI
only if they all said yes, every global's `PostAI`. `NPC.GetIncomingStrikeModifiers`:
`FinalDamage` times `takenDamageMultiplier`, the defence, `Knockback` times `knockBackResist`,
`DisableKnockback()` at exactly 0, then `NPCLoader.ModifyIncomingHit`.
`HitModifiers.GetDamage`: the source damage, the variation, the defence, the crit or not,
`FinalDamage` last, then the clamp. `GetKnockback`: none if disabled. `NPC.StrikeNPC`: nothing
under 1 damage; `justHit`; the life (off the whole, by `realLife`); the knockback (eased over
8, 10, 12 and 14, never over 16, times 1.4 for a crit, in full only when the damage is over a
tenth of its life, a fifteenth in Expert); then `HitEffect`; then the death.
`Player.ProcessHitAgainstNPC` and `Projectile.Damage` call `StrikeNPC` before the hooks for a
hit landed (`NPCLoader.OnHitByItem`, `OnHitByProjectile`). `Player.ApplyDamageToNPC`, then
`StrikeNPCDirect`, which sends the strike. `ScaleStats_ApplyGameMode` scales `knockBackResist`
by 0.9 and 0.8. In `Projectile.Damage` a hostile shot's `penetrate` is taken down for four
types only. `NPC.gravity` is `vanillaGravity` times `GravityMultiplier`. The Hungry
(`aiStyle` 29) goes by `Main.wofNPCIndex`. `ContentSamples.Initialize` runs before any mod's
`PostSetupContent`.

**Found on the way.**

- **The Megashark's musket balls can be parried now**, by the bullet rule: it fires
  `ProjectileID.Bullet`, whose `penetrate` is 1. Its High Velocity rounds cannot be.
- **Nothing else of the mod's own is reached**: every other NPC of the mod is a boss or does
  nothing by touch, and no other hostile shot of the mod's is solid by the rules (White
  Death's, the Queen Slime's core, Skeletron's, Amalgaermis's hands and ash).
- A minion's hit is a hit: a summoner's minions will take the riposte before the summoner
  does.

**Mine, not asked for** (all in `RELEASE_TODO.md`): every number in the table marked mine; an
enemy having to be attacking to be parried; a staggered enemy slumping and falling; nothing
being able to parry what is already staggered; the config, the five `Mod.Call` words, `/parry`
and the line in the log; the camera for a riposte; Amalgaermis's bones and skulls; The Hungry
counted as a part of the Wall; the Etherian Goblin Bomber counted as a bomb; the seven lines
of the Iron Flow's tooltip about what a parry does.

**Not built**: any weapon or shield parrying with its own ticks, and a key for the ones whose
right click is taken (§16.26's fourth). The Iron Flow is still the only thing that parries.

**Not yet confirmed in game:** all of it. Riskiest: an enemy of vanilla's whose AI does not
take being stopped for two seconds (none is known; the list for those is
`PADStaggerNPC.NeverStagger`, the config's, and `Mod.Call`); how the push looks on slopes and
at steps, where the tiles stop it short; the riposte's knockback, which is the game's at four
times and throws a light enemy hard; 'Dame' Quilt turned aside, which was only read; the
sounds, which have not been heard; two machines.

**The tools are not in the mod folder.** The stand-ins, the two programs that run the compiled
rules over vanilla, the storyboard and the script that drew it lived in the session's
workspace and went with it.

---

## 16.31 Later in session 7: the parry's stagger: every second parry in the air, every third on the ground

The ask, in the user's words:

> for the parry why is it staggering after every parry, make it stagger every 2 parries for
> flyiing enemies and 3 parries for ground enemies

**Why it did.** §16.30 staggered an enemy at the first parry if the game lets it be knocked
back, which is 353 of vanilla's kinds, and at the second if not. That line was mine, not asked
for.

**What it is now.** Every enemy a parry turns aside is still struck and pushed back at every
parry. It is staggered at every second parry of it if it flies and every third if it does
not (`Parry.FlyingParries` 2, `Parry.GroundParries` 3, read by `Parry.StaggersEvery`). The
game letting it be knocked back or not no longer comes into the count: light and heavy are
alike. Their difference is only the riposte's knockback, which is the game's.

**How it was read.**

- *Flying*: it has no weight (`NPC.noGravity`), as it is at that parry. A Demon Eye, a bat, a
  flying weapon, a fish in water. One that climbs or swims part of the time is counted by
  what it is doing when it is parried. **Mine.**
- *Every 2, every 3*: a count on the enemy, kept where the world is decided
  (`PADStaggerNPC.TakeParry`). It does not run down between parries, as before, and starts
  over at each stagger: the third, the sixth, the ninth. **The count not running down is
  still mine.**
- *'Dame' Quilt* is on the ground: every third parry. Her NPC has no weight of its own only
  because Triunfo carries her, so the mod says which she is (`Parry.AddNPC(..., flies:
  false)`, a new last argument; left out, the weight says). **Mine**: say if she should be
  two.
- *A flying weapon* (the Enchanted Sword, the Cursed Hammer, the Crimson Axe) flies: its
  stagger, thrown back spinning and open to the riposte, is on every second parry. The first
  strikes it and pushes it back like anything else. (Vanilla's own AI puts it in its spin for
  any hit in flight, so with a weapon given it spins after the first too; it is only not
  open.)
- Worms, segments and the lists' "never staggered" are never staggered, as before, and
  giants are not turned aside at all.

**One thing taken out.** With a server, the machine that parried a flying weapon used to throw
it back at once, ahead of the server's word. Whether a parry staggers is now a count only the
server has, so that machine decides nothing: it tells the server, as for any enemy, and the
weapon cannot touch that player for the 45 ticks meanwhile.

**The user's own edit, kept.** Between §16.30 and this, the Iron Flow's tooltip in the `.hjson`
was rewritten by hand to one line, "Right click to parry". It was left as it is: the only
change made to that file is the tooltip of the config's "Enemies always parried" list.

**Changed.** `Parry.cs` (the two numbers in place of `HeavyParries`, `StaggersEvery`, `flies`
on `AddNPC`, `Throw`, the client's side of `Turn`, the line in the log), `ParryRules.cs` and
`PADStaggerNPC.cs` (their notes), `ParryCommand.cs` (`/parry` says "every second parry
staggers it (it flies)" or "every third"), `DonaQuinn.cs` (`flies: false`),
`DonaQuinn.Fight.cs` and `IronFlow.cs` (their notes), the `.hjson` (one tooltip).

**How it was checked.** Nothing was run in the game. The stand-ins of §16.30, with the scenes
that lean on a stagger given the parries that come before it, and a new scene for the count:
232 checks, all passing. Six parries of a Zombie: the third and the sixth stagger it, six
blows. Four of a Demon Eye: the second and the fourth. A Paladin: the third, 600 ticks after
the second; then two that do not and one that does. A flying weapon: struck and pushed by the
first, thrown back spinning and open by the second. A boss the mod speaks for, with no weight
of her own and `flies: false`: the third. The whole mod compiles against the real 1.4.4
assemblies: 0 errors, 10 warnings.

**Not yet confirmed in game:** all of it, as §16.30.

---

## 16.32 Later in session 7: 'Dame' Quilt's lance is done with a player once it has hit them

The ask, in the user's words:

> why is dame quilt can still be parried after attacking me? is it because the spear too big?
> how about disable spear hit on player after an attack in one direction so it cant be parried
> when its alr hit the player

**Why she could be.** Two things, both in `LanceStrike`. The lance is long (76 pixels of
sprite, and its hit runs from 10 past her fist to 10 past the point, 24 thick) and she rides
on past at up to 7.2 pixels a tick, so it is still across the parry's arc for some ticks
after it lands. And the parry was asked before anything else every tick of the charge, ahead
even of whether the player had just been hit: nothing remembered that this charge's lance had
already landed. (Triunfo's ram and the thrown lance did remember: `rammed`, and the lance's
own `localAI[0]`.)

**What it is now.** As asked: a new flag, `lanced`, each machine's own for its own player,
beside `rammed`. It is set when the lance lands on that player and cleared when she next
begins a charge or a ram. While it is set `LanceStrike` does nothing: no second hit from that
charge, and no parry of it.

**What else that changes.** The lance hits once a charge. Before, only the player's own
moment of immunity stood between them and a second hit from the same pass.

**Not changed.** A parry before the hit works as before. An ordinary enemy that has just hit
a player can still be parried while it keeps coming: only her lance was asked about.

**How it was checked.** Read against the phases beside it; the whole mod compiles against the
real 1.4.4 assemblies, 0 errors, 10 warnings. Her code is not in the stand-ins, so nothing
was run. **Not yet confirmed in game.**

---

## 16.33 Later in session 7: the Golem's fist parried: sent back into the Golem

The ask, in the user's words:

> Make it so you can parry golem's fist, making it goes back (turn 180 degrees, back to its
> core causing massive damage to both the hand (1/7 maxhp) and the core(1/21))

and, while it was being read:

> for this guy no stagger tho

**What it is.** A Golem fist punching at a player can be parried. It takes the parry's blow as
anything does, is turned half a turn, and flies back to its place on the Golem. When it gets
there the fist loses a seventh of its full life and the Golem a twenty-first of the Golem's.
It is never staggered, and so never open to the riposte; it is not pushed either. Seven
punches parried break a fist; both fists broken that way have cost the Golem fourteen of its
twenty-one parts.

**The fist's AI, read from the 1.4.4 assembly** (`NPC.AI_047_GolemFist`; both fists, types 247
and 248). Its place is the Golem's middle plus the Golem's speed, 9 up, and 84 left (the left
fist) or 78 right, each times the fist's scale. `ai[0]` is its state:

- 0, at the Golem or going back to it. It goes through tiles and flies at its place at 14 a
  tick (3 more under half its life, 3 more under a quarter, 8 more once the Golem is below
  full life; times a quarter of balance plus 3; at most 32). The tick it is within 12 plus
  that speed, its speed is set to exactly the way left, it is turned to 0, and `ai[1]` counts
  up by balance (more by the same three tests, ten times balance for the last); at 60 it
  winds up, if the player is on its side.
- 1, winding up: held at its place for 30 ticks, then thrown at the player at 12 (4, 4 and 10
  more by the same tests; at most 48).
- 2, the punch. It ends, back to 0, at 700 from its place or when a tile stops it (tiles only
  stop it once it has passed the player).

If there is no Golem (`NPC.golemBoss` under 0) the fist kills itself.

**How it is done.** One new file, `Content/NPCs/GolemFistParry.cs`: a `GlobalNPC` on those two
types only.

- It is listed with `Parry.AddNPC` for both, parried only in state 2 and only while there is
  a Golem. `Parry.Of` asks the mod's own list before the rules, so the rules' "a part of a
  boss, never" does not come into it; the two stay on `ParryRules.BossLimbs`, with a note.
- Parried (where the world is decided): marked as sent back, state 0, `ai[1]` 0, through
  tiles, its speed turned toward its place. The game's own AI flies it home from there, at
  the game's own speed for that.
- Each tick it is on its way, after its AI has run, it is pointed half a turn from how the
  game points a fist going home (worked out from its speed, not added to what it has, so a
  tick its AI does not run turns it no further): knuckles first.
- The tick its AI sets it down (its next step ends at its place; any other tick it ends 12
  short or more), the mark is cleared, the Golem is struck for its full life over 21 and the
  fist for its own over 7, each a flat `NPC.StrikeNPC`, sent to everyone on a server.
- If the Golem goes while it is on its way, the mark is cleared and nothing is struck.

**The numbers, in an ordinary world.** The Golem has 15000: 714 a punch. A fist has 10000:
1428, on top of the parry's blow. In Expert and Master they are sevenths and twenty-firsts of
what the two have there.

**How it was read. Say which meant something else.**

- *"golem's fist"*: both, and only the punch in flight. The fist at the Golem's side, winding
  up, or going home by itself cannot be parried, as before. **Mine.**
- *"the core"*: the Golem's body (type 245), the one with the boss's life. Yours, as I took
  it.
- *The Golem takes it while its head is on.* The game lets nothing hurt the body until the
  head is off (`dontTakeDamage`, set each tick by its AI). `NPC.StrikeNPC` does not ask that,
  and this strikes it all the same: otherwise the twenty-first would do nothing for the whole
  first half of the fight. **Mine.** It cannot kill the Golem by itself: fourteen
  twenty-firsts at most.
- *When*: the damage is at the landing, not at the parry. Yours ("back to its core causing").
- *Flat*: no armour taken off, no critical hit, no variation. **Mine.**
- *"turn 180 degrees"*: the fist is drawn turned half a turn from how the game points it,
  for as long as it is on its way, and goes home by the game's own way home. Yours.
- *"no stagger"*: no count is kept for it at all, so nothing ever opens it to the riposte.
  Yours. That it is not pushed either is **mine** (it is sent home instead).
- *The parry's blow* still lands on the fist at the parry, as on anything turned aside.
  **Mine**: nothing was said against it.
- *The wait before its next punch starts from nothing* after a landing (`ai[1]` 0). The game
  itself carries the punch's own ticks over into that wait. **Mine.**
- *Whoever parried it is marked as having had a hand in the Golem* (`NPC.PlayerInteraction`,
  which marks every part of the Golem at once), so the Golem's loot is theirs too if this is
  what kills it later. **Mine.**

**What follows from it, which is the game's doing.** The game reads a Golem below full life as
further into the fight. From the first landing on: a fist flies home at 22 instead of 14 and
punches at 22 instead of 12; it waits 6 ticks before winding up instead of 60; and the
Golem's own count toward its next jump (`NPC.AI_045_Golem`) goes up by 2 a tick instead of
1 while its head and both fists are on. In vanilla none of that starts until the head is
off. Not changed, and not asked about: say if the first half of the fight
should be kept as slow as it was.
*[Superseded later in session 7: while its head is on, the Golem and its fists keep the pace of a Golem at full life, whatever the fists have cost it. See §16.34.]*

**In `Parry`, for it.** `Parry.AddNPC` has two more last arguments: `reels` (false: never
staggered, no count kept; `PADStaggerNPC.CanReel` now asks it through `Parry.NeverStaggers`)
and `told` (what a parry does to it, in words, for `/parry`, where the usual words would be
wrong). `Parry.Told(type)` reads it; `ParryCommand` uses it.

**Vanilla's counts in the game** are now 353 light, 61 heavy (24 staggered, 35 worms, the 2
fists), 25 giants, 75 never. The table the rules alone make is unchanged: it was run again
and is the same file, byte for byte.

**With two machines.** The machine that parries decides nothing, as for any enemy: its blow is
its own hit and is sent, the server is told, and the fist cannot touch that player for 45
ticks. The server sends the fist back, and at the landing makes both strikes and sends them
as the game sends a hit. One bit goes with the fist (sent back or not). Every machine shows
the landing itself, off its own copy of the fist getting there, once a trip: a thump, a ring,
the temple's stone, a shake within 1400 pixels.

**How it was checked.** Nothing was run in the game. The stand-ins of §16.30 now take in the
new file, whole and unchanged, with the fist's AI written out from the assembly as above and
a Golem that stands: 306 checks, 74 of them new, all passing. Not parried at the Golem's
side or winding up; parried in the punch; let through, it lands for 59, goes home, and costs
nothing. Parried: the blow (16, the sword's 30 less half of 28), sent home at 14, half a turn
from the game's pointing on every tick of the way, no stagger, no push; the landing 16 ticks
on: 714 off the Golem though nothing else can hurt it, 1428 off the fist, once, then it
punches again at 22. Seven parries break it, the Golem at 10002. The right fist from the
other side, at its own place. A punch parried one step out lands the same tick. The Golem
gone mid-way: nothing struck. Stopped mid-way by the Greatbow's stagger: turned no further,
and it lands when let go. Expert's numbers. The server: sent back on a client's word,
both strikes sent, nothing on a word about a fist at the Golem's side. A client: nothing
decided, the landing shown once whatever the server repeats, shown again for the next trip.
The whole mod compiles against the real 1.4.4 assemblies: 0 errors, 10 warnings. The review
was my own.

**Changed.** New: `Content/NPCs/GolemFistParry.cs`. `Parry.cs` (`AddNPC`'s two arguments,
`NeverStaggers`, `Told`), `ParryRules.cs` (a note on `BossLimbs`), `ParryCommand.cs` (the
mod's own words for a kind).

**Not yet confirmed in game:** all of it. The sounds of the landing (`DD2_MonkStaffGroundImpact`
and `Item14`, both pitched down) were picked by what they are, not by ear. How the fist looks
turned half a turn (it is upside down as well as about) has not been seen.

---

## 16.34 Later in session 7: a parried fist no longer hurries the Golem while its head is on

The user, after trying §16.33:

> does it agggro the fucking golem or something?

It did, in the way §16.33 had set down under "What follows from it" and left in: the first
fist sent back takes the Golem below full life, and the game makes a Golem below full life
faster. Asked which they wanted, of three (keep the damage and keep it slow; no damage to
the Golem until the head is off; leave it), the user chose the first.

**What reads the Golem's life, from the 1.4.4 assembly.**

- `NPC.AI_047_GolemFist`, three places, all "the Golem is below full life": 8 more speed home,
  10 more speed in the punch, and ten times as much a tick toward winding up.
- `NPC.AI_045_Golem`, its own: its count toward a jump (1 more a tick below full, 4 more
  below half, 8 more below a third), its jump (below full it is scaled by balance), and a
  cap on its speed across (1 more below full, below half and below a quarter).
- The head while it is on (`aiStyle` 46, in `NPC.VanillaAI_Inner`) reads only where the body
  is, never its life. The head once it is off (`aiStyle` 48) reads its life a great deal;
  that is the second half of the fight and is left alone.

Neither AI writes the Golem's life.

**What it is now.** One new file, `Content/NPCs/GolemPace.cs`: a `GlobalNPC` on the Golem and
its two fists. While the head is on (the body's `dontTakeDamage`, which its own AI sets each
tick to whether a Golem Head is alive) and the Golem is below full life, its life is put by
in `PreAI` and its full life put in its place; `PostAI` puts it back, less anything it lost
in between, and never at less than 1 while it lives. So those two AIs see a Golem at full
life, and nothing else does: hits, the bar, what is sent and what is drawn all come outside
an AI. `NPCLoader.NPCAI` runs every global's `PreAI` whatever the others answer and always
runs `PostAI`, so the two always pair; `PreAI` also puts back anything left over first.

The fist's landing strikes the Golem from inside the fist's own `PostAI`, which runs before
`GolemPace`'s. It asks `GolemPace.Restore()` first, or its twenty-first would be struck off
the full life and lost when the real one was put back.

**What that keeps and what it does not.**

- Head on: a fist sent back costs the Golem its twenty-first and the fist its seventh as
  before, and the fight stays at the pace it had: fists home at 14, punch at 12, 60 ticks
  between punches, the jump count at 1. A fist's own life still makes that fist faster
  (below half and below a quarter of its own), as in vanilla.
- Head off: nothing is put by. The Golem is as fast as the life it has left makes it, which
  after parried fists is less life, and so faster, than vanilla would have at that moment.
  **Mine**: "while its head is on" was the question's wording and the answer's.

**How it was checked.** Nothing was run in the game. The stand-ins take in the new file,
whole and unchanged, with the globals' hooks in the game's order: 320 checks, 14 of them new,
all passing. Head on and hurt: the Golem's AI and its fist's are shown 15000 and it has 9000
after; what it loses inside its AI it has lost; its AI stopped by something else, nothing is
lost or gained; head off, both are shown what it has. A parried fist with the head on: the
Golem at 14286 after, 60 ticks to the next wind-up, the next punch at 12; seven parries
still break the fist and leave the Golem at 10002. With the head off: 6 ticks, and 22. The
whole mod compiles against the real 1.4.4 assemblies: 0 errors, 10 warnings. The review was
my own.

**Changed.** New: `Content/NPCs/GolemPace.cs`. `GolemFistParry.cs` (asks `GolemPace.Restore()`
before it strikes; its notes).

**Not yet confirmed in game:** all of it.
