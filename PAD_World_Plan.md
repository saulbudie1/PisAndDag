# PisAndDag — Extra Large World Plan

> The map an Extra Large world is generated to. Companion to
> `PAD_Grace_Structure_Plan.md`, which builds the right-hand seventh of it, and to
> `ExtraLargeWorld.cs` / `ExtraLargeOcean.cs`, which already do the size and the ocean.
>
> World: **12600 × 3000**. Everything below is in tiles, x from the left edge.

---

## 1. The three decisions this plan makes

1. **The right ocean is gone.** In its place, a dead flat plain running from the east
   edge inward one seventh of the world — 1800 tiles — with the Kingdom of Grace on it.
   The world has one ocean, on the left, and it is twice the ordinary size.
2. **All three infectious forces are in the ground from the first minute** (§XXVI): the
   one chosen at world creation as a full biome, the other evil as a hollow sphere two
   thirds of the way down to the Underworld under the beach, and the Hallow as a sealed
   pocket in the cavern. The Seal suppresses them; it does not mean they are not there.
   Two of the three have been cut into by somebody, in opposite directions.
3. **Every biome stays the size it is in a Large world.** The extra 4200 tiles of width
   is not stretch, it is room. More fits, and nothing is thinner.

---

## 2. Vertical bands

Layer boundaries are fractions of world height, so they come up 1.25× a Large world on
their own. The Underworld is a fixed 200 rows and does not.

| band | y | notes |
|---|---|---|
| Space | 0 – 160 | `worldSurface * 0.35` |
| Sky | 160 – 450 | 10 floating islands, each Large-sized |
| Surface | ≈ **450** | the Grace Plain is a pitch 58 – 84 tiles *over* the local hilltops |
| Underground (dirt) | 450 – 1050 | `rockLayer` ≈ 1050 |
| Cavern (rock) | 1050 – 2800 | |
| Underworld | **2800 – 3000** | fixed 200 rows, unchanged |

---

## 3. Horizontal band map

| x | width | region |
|---|---|---|
| 0 – 760 | 760 | **Ocean.** The world's only one. Water to ~480, sand to ~700, biome to 760. Twice ordinary length, twice ordinary depth. |
| 600 – 1300 | 700 | **The Ball**, deep: an irregular hollow shell centred (950, 2020), outer radius ~350, hollow to ~170. Under the beach, two thirds of the way down. |
| 1400 – 1750 | 350 | **Dungeon.** `dungeonSide = -1`, at the seaward end where vanilla likes it. |
| 1750 – 2400 | 650 | **Forest.** |
| 2400 – 3400 | 1000 | **Primary evil.** The one chosen at creation, vanilla shape and size. |
| 3400 – 4700 | 1300 | **Desert**, with the Underground Desert beneath. |
| 4700 – 6500 | 1800 | **Forest.** Spawn at **6300**. Graveyard shoreline, Mors monument. |
| 6825 – 7475 | **650** | **Snow**, five hundred and twenty-five tiles east of where the player wakes up. Half a Large one — the only biome in the world that is not full size, and it is the one the player meets first. It was 975 and it started at 6500, which put the player's first morning inside it; the third that came off came off the spawn end. |
| 7475 – 8040 | **565** | **Forest.** The gap, halved. Enough to see the jungle coming and not enough to rest in. The jungle edge of the snow never moved, so this is unchanged. |
| 8040 – 9440 | 1400 | **Jungle**, `jungleOriginX = 8740`, with the Temple beneath at 8500 – 9000. |
| 9440 – 10330 | **890** | **The Cleared Ground.** In the cavern below it, wherever the rock is most open: **the Sealed Pocket**, ~200 tiles across. |
| 10330 – 10800 | **470** | **The Wasted Mile.** Goblin siege works, two thirds of its old width. |
| 10800 – 12600 | **1800** | **The Grace Plain.** |

Total 12600. The Grace Plain is exactly `maxTilesX / 7`.

**The east half is a corridor, and then it stops being one.** Spawn, snow, a thin gap,
jungle — four biomes inside two thousand nine hundred tiles, each handing over to the
next with nowhere to rest. West of spawn the world is ordinary and spread out; east of it
the world is a sequence, and the player is meant to feel that without being able to say
why.

Then the corridor opens onto **the Cleared Ground**, and it is empty.

Eight hundred and ninety tiles of it. This is the room the shorter snow and the halved
gap paid for, and it is the most important empty space in the world: the Curtain stands
a hundred and sixty tiles above the plain at the far end of it, and the player crosses
the whole width with the wall growing in front of them and nothing else to look at.

It is empty for a reason and the reason is on the ground. An army lays siege by clearing
its field of fire first, and this one did: stumps, spoil heaps, old cut lines, no
standing timber. Three hundred years and nothing has grown back, because nothing in this
era finishes — including a clearance. Nobody has to say any of that. The player walks
across three hundred years of somebody else's preparation and arrives at a wall that
held.

---

## 4. The three forces

§XXVI gives each of them an absolute. The build should let the player meet all three
before the Seal breaks and understand none of them.

**Three holds, one each** — §XXVI, *The Three Holds*. The geometry below is not three
ways of putting a biome somewhere; it is three different containments, and the difference
between them is the lore. One is **pressed** on every side with nowhere to spread. One is
**warded**, with nothing built around it, because anything built would be corrected. One
is **left breathing** in the open, because a force with nothing to push against stops
pushing and the lock needs all three still pushing. Everything in this section that looks
like an aesthetic choice — the shell that is 180 thick, the pocket that is painted into
rock the player was already standing in, the surface evil left entirely to vanilla — is
that distinction, and none of it is ever explained in game.

Nothing in code enforces the hold. All three spread by vanilla rules, break by vanilla
rules, and answer to the Wall like they always did. The hold is what the shapes say.

### The primary evil — x 2400 – 3400 — *left breathing*

Whatever the player picked at world creation. Vanilla generation, vanilla size, vanilla
chasms or pits. Nothing to do, and that is the point: it is the one nobody held.

### The Ball — centred (950, 2020), outer radius ≈ 350, hollow to ≈ 170 — *pressed*

The other one. Not a biome and not a blob: a **shell**, roughly seven hundred tiles across
and a hundred and eighty thick, wrapped around open air, with thin arms reaching out of it
into the rock in every direction.

| | |
|---|---|
| x | **950**, fixed — under the beach, the only side of the world that has one. Constraint: `x ≥ R + 90`. |
| y | `worldSurface + (UnderworldLayer − worldSurface) × 2 / 3` — **≈ 2020** here, 1592 in a Large world |
| outer radius | **350 ± 13 %** |
| inner radius | **170 ± 26 %** |
| shell span | x ~600 – 1300, y ~1670 – 2370 |
| with arms | x ~250 – 1650, y ~1520 – 2740 |

- Deep. Two thirds of the way from the surface to the Underworld is an expedition, not a
  detour, and it puts the thing far below the ocean rather than in it.
- **Nothing about it is drawn with a compass.** The outer surface and the hollow are two
  separately rolled radius profiles — a few sine terms at whole-number frequencies,
  normalised so the roughness is the number that was asked for and the shape still closes
  after a full turn — with a tile of grain laid on top of each. The thickness is different
  everywhere round. A perfect annulus was the single loudest tell that a mod put it there.
- **The shell is the biome.** Six to ten chasms, at full vanilla difficulty, go in the
  thickness of the shell. None of them reaches the middle.
- **The floor is a bank, not a floor.** What the player drops onto is a thick jagged rise
  of the same stone, a random walk with shelves in it, and three to six basins of standing
  water cut into the top of it. Nothing was laid. Whatever emptied this place did not tidy
  up after itself.
- **The arms grow through rock and eat nothing anybody built.** An arm is a six-hundred
  tile random walk and the first version of it came out of the Dungeon's ceiling as a
  crimson vein through the pink brick. What a Blob may convert is now a whitelist of
  ground — dirt, stone, clay, sand, mud, ice, granite, marble, ash, the two evils' own
  stone and grass — and a matching list of cavern backgrounds. Brick, wood, dungeon,
  temple, ore and anything framed are passed over. A whitelist rather than a blacklist,
  so a tile nobody thought about is left alone instead of chewed.
- **It carries its own weather.** Evil grass on every open face, vines hanging off the
  undersides, thorns and plants on the tops, and fourteen to thirty veins of Demonite or
  Crimtane in the thickness. None of it was planted; the shell is the biome, so the biome
  grows on it.
- **One shaft down into it**, cut, not natural, entering the shell from above. That is how
  the player gets in and it is the first thing that says somebody was here before them.
- **It does not throw a V to the surface at hardmode.** Every other seed in the world
  does; this one is two thousand tiles down and it stays down. Its spread is the arms
  growing — longer, more of them, sideways and downward through the rock — and it never
  breaks the surface on its own. The beach above it stays clean, and the player who has
  been walking over it all game goes on doing that with something moving underneath. It
  will meet the other fronts from below, in the caverns, on its own schedule.

#### The arms

The sphere does not have a clean edge. It has taken hold of the rock around it.

| | |
|---|---|
| count | **48**, spaced around the shell with jitter so no two are evenly apart |
| origin | the outer surface, r = 350 |
| length | **150 – 400** tiles, so they reach r 500 – 750 |
| width | **6** tiles at the root, tapering to **1** at the tip |
| path | a random walk with a slow angular drift, so each one curls rather than radiates straight |
| material | the same tiles as the shell |

They are thin, they are irregular, and there are a lot of them. The shape they add up to
is not a planet and not a cave. It is a grip.

Two hard limits. **No arm goes above y 1500** — the thing does not reach for the surface,
and a player who has been walking over it all game must have no way of knowing. **No arm
comes within 60 tiles of the Dungeon** at x 1400 – 1750, which is the one structure in
range.

When the Wall dies the arms grow again: **twice as many, half again as long and half
again as thick** — 225 to 600 tiles instead of 150 to 400, nine tiles at the root instead
of six. The old arms are swallowed by the new ones going out past them. Both limits still
hold, so what the player gets is a thing that is visibly bigger every time they come back
down and has still never broken the surface.

#### The twelve

In the hollow there are **twelve** of them — Shadow Orbs if the Ball is Corruption,
Crimson Hearts if it is Crimson.

They are not scattered where they grew. They are **set out in a ring**: a true circle,
evenly, at a radius of 105 from the centre. Each one is set into a knuckle of the same
stone with an alcove cut in front of it — above the bank that reads as a nodule hanging
in the dark, and in the lower half of the ring, where the bank has already filled the
space, as a niche somebody opened. The ring is the one regular thing in the room, which
is the whole point of it. Vanilla never gives the player more than three together and
never arranges them.

**Which of the two it is came out backwards twice, and the second time settled it.**
`WorldGen.AddShadowOrb` reads `if (WorldGen.crimson) frame += 36`, and by every reading
of the decompile frame 36 is the Crimson Heart. It is not — what frame 36 draws is the
Shadow Orb. Watched in two worlds, with the mod's own log as the witness: a corruption
Ball with the flag false gave a Crimson Heart in ebonstone, and a crimson Ball with the
flag true gave a Shadow Orb in crimstone. Both wrong, each the other way round, which is
what an inverted flag looks like and nothing else does. So the call is handed the
**opposite** of the Ball's evil, on purpose, and the log line reports the evil the Ball
*is* — if it ever flips back, the log and the screenshot disagree again and that is the
test.

**The frame is still not ours to write.** Shadow Orbs and Crimson Hearts are the
same tile with two frames, and the only thing separating them is a frame index. Writing
that index by hand — reading `WorldGen.CheckOrb` and `Place2x2Style` and reasoning about
which way round 0 and 36 go — produced Crimson Hearts in a Corruption shell, in a world
whose logs said Shadow Orbs. So the frame is left to vanilla: `WorldGen.crimson` is set to
the **Ball's** evil for the length of one `WorldGen.AddShadowOrb` call and put straight
back. Whatever vanilla does for a world of that evil is what happens here, by
construction, and there is no constant left to get backwards.

**Six of the twelve are walled in**, because the lower half of the ring is inside the
bank. Each of those gets a **single tile of air straight up** to whatever is open above
it — the chamber, a chasm, a pool. One tile wide: enough to see the glow through, narrow
enough to read as bored rather than built. The flue stops at the first open tile, so the
six hanging in the chamber never get one. Water basins are kept off those columns; a
basin cut over a flue drains down it and leaves a dry hole where the pool was.

- Twelve is **four boss summons** and most of the pre-hardmode evil loot table in one
  room, which is a very large amount of power sitting in one place.
- What gates it is the journey. Two thirds of the way to hell, through a hundred and
  seventy-five tiles of evil biome with a full-strength chasm in it, at a depth the player
  reaches when they reach it and not before. The room is a vault and the shell is the
  door.
- Breaking them is also four times the meteor pressure vanilla expects. Worth a look at
  whether the world can take that many.

**[OPEN]** Who set them out, and whether twelve is a count or a ration. Nothing in the
chamber answers it, and the shaft is old enough to be anybody.

### The Sealed Pocket — somewhere under the Cleared Ground, ~200 tiles across — *warded*

The Hallow, three hundred years before the Hallow should exist.

- **It is a patch of the wrong rock, not a room.** Somewhere in the cavern under the
  Cleared Ground there is a cave like any other cave, except that the stone around it is
  Pearlstone. Nothing was built, nothing was dug and nothing new was invented: the pass
  picks the most open spot it can find that nothing else has claimed, and repaints the
  ground that was already there — Stone to Pearlstone, sand to Pearlsand, cave walls to
  Hallow walls — in one small continuous mass with a rolled, ragged edge that dissolves
  back into ordinary grey over the last fourteen tiles.
- A cave is the point. A wall of Pearlstone the player can only find by mining into it is
  a wall of Pearlstone nobody ever sees.
- **It is not the Seal.** §XXVII [LOCKED] has exactly one Seal, it is in the Underworld,
  and it *is* the Wall of Flesh — three forces bound into one body holding each other
  down. This is something else: one force, alone, in a hole in the ground, a thousand
  tiles up. Nothing in the build says whose and §5 of `PAD_Grace_Structure_Plan.md` is not
  allowed to either.
- **It spreads when the Wall dies**, along with everything else, and the player is
  standing in a world where the pretty one was always there.

**[OPEN]** Whether anything at all marks it as deliberate, or whether the right answer is
that the Hallow is simply *in the ground here*, has always been, and no hand put it there.

---

## 4a. The Shimmer, under Grace

The world's one seam of Shimmer comes up under the Kingdom of Grace.

**It is not hidden and it never was.** §XI [LOCKED]: the Archmage did not take Shimmer
away — he locked what it *does*. The substance is still in the ground in the quantities
it always was, and what is gone is the question it asks. So the pool under Grace is not a
fugitive god in a cellar. It is the same liquid with nothing behind it: transportable,
storable, ladlable, ordinary. Water.

Which is why the Font works at all. A liquid that changes what things *are* could not be
carried to a table in a ewer by a servant. An inert one can, and for three hundred years
was.

The timing is what makes it land. Grace's good years are **before** the Working; the lock
comes **after** it. So the kingdom drank the live thing the whole time it was a kingdom,
and whatever the bloodline became it actually became. The favour was real. It simply was
not divine, and the difference was drained out of it afterwards by a man who never came
to the table.

What the player finds is a sample. Somebody kept it; the ewers are still on the trolley,
and she is still in the room. §XIX, *The Cupbearer* — the servant who carried it and
drank the dregs, small, gold, arrested three hundred years into becoming something else,
and the first boss after the Moon Lord. The Font is her arena. Nothing in generation has
to know that yet; it is built as a room either way.

This does **not** answer §XI's open question about where the rest of it is, because there
is no "rest of it" in the sense of a hidden reservoir — the open question is where the
*properties* went, and nothing in this world plan touches that.

**This falls out of the layout for free.** The Shimmer pass sends the pool to the side
opposite the Dungeon — `dungeonSide < 0` puts it at `rand(maxTilesX * 0.89, maxTilesX −
200)`. With the Dungeon forced left (§3), that is **x 11214 – 12400: entirely inside the
Grace Plain**, every time, with no hook at all. Depth lands in the upper cavern, which is
where the Rootworks are.

So Grace is built **around** the vein rather than the vein moved under Grace:

- The Grace passes run **after** `Shimmer` and read `GenVars.shimmerPosition`.
- The **Rootworks** are the pool's own cavern, wherever it came out.
- The **Font** is a fixed room under the chapel, and a cut shaft joins it to the pool.
  The shaft's length is whatever the roll gave, which means the walk from the chapel down
  to the water is a different length in every world — three hundred years of carrying it
  further than anyone remembers agreeing to.

---

## 5. What generation has to be told

Everything horizontal in vanilla is decided inside the **Reset** pass — `dungeonSide`,
`dungeonLocation`, `jungleOriginX`, `snowOriginLeft`, `snowOriginRight`,
`leftBeachEnd`, `rightBeachStart`. That makes one seam enough for the whole layout.

### Pass list changes

| # | where | pass | what it does |
|---|---|---|---|
| 1 | *after* `Reset` | **PAD Layout** | overwrites the origins above with §3's numbers — `dungeonSide`, `dungeonLocation`, `snowOriginLeft/Right`, `jungleOriginX` — and squeezes the Grace-side beach to a sliver |
| 2 | *around* `Dunes` | **bracket** | the desert is held to the beach-side band; see §5b |
| 3 | *around* `Corruption` | **bracket** | the primary evil is held to x 2400 – 3400; see §5b |
| 4 | *after* `Gem Caves` | **PAD The Ball** | the shell, its bank, the chasms, the ring and the shaft |
| 5 | *after* `Gem Caves` | **PAD Grace Plain** | raises the pitch over the country at x ≥ 10800 |
| 6 | *after* `Gem Caves` | **PAD Sealed Pocket** | repaints a cave under the Cleared Ground |
| 6a | *after* `Gem Caves` | **PAD Cleared Ground** | takes the floating islands off the Cleared Ground, with the leaf walls under them, and cuts any living tree on it to a stump; levels nothing; see §5c |
| 7 | *after* `Shimmer` | **The Rootworks** | *[not written]* reads `GenVars.shimmerPosition`, dresses the pool's cavern |
| 8 | *after* `Micro Biomes` | **PAD Wasted Mile** | goblin siege works |
| 9 | *after* `Micro Biomes` | **Grace** | *[not written]* the capital, and the shaft down to whatever the Shimmer roll gave |
| 10 | *before* `Final Cleanup` | **PAD Grace Cleanup** | strips the growth off the plain and off the Cleared Ground, leaving the stumps, and leaving what stands inside a living tree |

All three set pieces run **after `Gem Caves`**, which is the last pass that carves caverns
at scale, and every one of them registers itself with `GenVars.structures` on the way out.
§5a says why neither of those is sufficient on its own. They run Ball → Plain → Pocket, so
the Pocket knows where the Ball went and can refuse to sit on it. The Cleared Ground's pass
runs ahead of the three of them, in the same place for a different reason: it has to come
after everything that carves and before everything that grows. It builds nothing, and
registers nothing.

### Killing the ocean — and the trap in it

The obvious move is to squeeze the Grace-side beach to zero: `rightBeachStart =
maxTilesX`, or `leftBeachEnd = 0` when the world is mirrored. Both look right and both
crash world generation.

The Beaches pass clamps its ocean start against those numbers and then **scans down the
column one tile past it** to find the ground, before it ever checks whether it has any
columns left to carve. `leftBeachEnd = 0` puts that scan at **x = −51**;
`rightBeachStart = maxTilesX` puts it at **maxTilesX + 50**. Either way it walks off the
end of the tile array — `IndexOutOfRangeException` inside vanilla, with a stack trace
that names no mod.

So the beach is squeezed to a **sliver** instead, sixty tiles, which keeps the arithmetic
in bounds and leaves about ten columns of water at the extreme edge of the world. The
Grace Plain pass buries those without being asked: at the seaward hundred and ten columns
of the plain — the Dry Shore, where §4 of the structure plan wants dry rock anyway — it
fills all the way to the rock layer instead of stopping at the ground.

The Ocean Caves pass needs no handling. It generates on the same side as the Dungeon,
and the Dungeon is at the ocean end by construction.

### Every made surface passes two rules

Both the Grace pitch and the Wasted Mile are laid out as a **height line first, tiles
second**, because the things that make ground look like ground are properties of the line.

1. **It comes from a smooth function, not a random walk.** Three or four sine terms,
   normalised to the rise asked for. What matters is the derivative: four tiles of swell
   over six hundred changes the height by a fortieth of a tile per column, so a one-tile
   step happens once in forty columns and never twice running. A random walk of the same
   amplitude steps *every* column, which is exactly what made the first pitch read as
   scree. Amplitude buys character, wavelength buys silk, and they are separate dials —
   the pitch is 4 over 320–900, the Mile is 5 over 55–180.
2. **No column sits more than a tile off its neighbours.** Anything steeper is filled, low
   side up, never high side down, so the ground gains rather than loses. One sweep each
   way settles it exactly; it runs twice because the second look costs nothing.
3. **Every surviving one-tile step is hammered.** A column standing a tile proud of a
   neighbour is poured into a half block with `WorldGen.PoundTile`, so the walk goes 500,
   500½, 501 rather than 500, 501. That is the whole difference between a staircase and a
   hillside, and it is the same tool the player would reach for.

The ramp onto the pitch is now as long as the climb needs — about 1.6× the height, because
a smoothstep's steepest point is one and a half times its average. Fixed at 230 it could
ask for more than a tile per column in the middle, and rule 2 would then quietly stretch
the ramp out across the flat ground to obey itself.

### The pitch sits *over* the country, not in it

The first build of the Grace plain took `Main.worldSurface`, went 48 tiles above it, and
stamped a flat slab from there down to the rock layer across the whole 1800 tiles. It
came out **below the surrounding hills**, because `Main.worldSurface` is where the
background stops being sky and is a long way under the tops of them, and the result read
as a trench of placed blocks cut through natural ground. It was the worst thing in the
world.

What it does now:

1. **Measure the ground**, not the layer. Walk the plain and 340 tiles inland, take the
   first solid tile in each column scanning down from `GenVars.worldSurfaceLow` — which
   vanilla records while laying the terrain and which is above every hill and below every
   floating island — and keep the highest.
2. **Put the pitch 58 to 84 tiles over that**, clamped so it is never within ten tiles of
   `worldSurfaceLow − 50`, which is the lowest a floating island is allowed to hang.
3. **Fill down from the pitch until it meets ground and stop.** Not to the rock layer. The
   country under the plain keeps its caves, its ore and its shape; the pass adds a lid over
   the top of it and nothing else. A hill under the plain is still a hill.
4. **Give the rim and the top a little grain** — a slow ±3 drift and a tile of noise — and
   make the way up at the inland end a run of broken steps rather than a smooth ramp.

The silhouette is then a plateau standing over the country, which is what a thing called
the Grace Plain should look like from the Cleared Ground.

### Size clamps — "same size as Large"

The existing rule in `ExtraLargeWorld.cs` — clamp the world-width term that feeds a
**size**, leave the ones that feed a **count** — extends to three more places:

| where | term | clamp to |
|---|---|---|
| `WorldGen.MakeDungeon` | `maxTilesX / 60`, `* 2`, `/ 100`, `/ 20` | done |
| `WorldGen.makeTemple` | `maxTilesX / 4200.0` | done |
| `WorldGenRange.ScaleValue` | `maxTilesX * maxTilesY / 5040000.0` | **4.0** |
| `WorldGenRange.ScaleValue` | `maxTilesX / 4200.0` | **2.0** |
| `JunglePass.ApplyPass` | `maxTilesX / 4200.0 * 1.5` | **3.0** |
| `HiveBiome.CreateHiveTunnel` | `maxTilesX / 4200.0` | **2.0** |

`WorldGenRange.ScaleValue` is the general scaler behind most of the gen ranges, so those
two lines carry the majority of it. The rest wants a sweep before this is called done.

Counts are left alone and come out 1.5× on their own: 10 floating islands, 3 living
trees, 37 gem caves.

---

## 5a. Not clashing with anything

Stolen from Calamity's Draedon labs, which solve this problem for six buildings dropped
into six biomes they did not author.

### What `StructureMap` actually covers

`GenVars.structures` is a public, non-readonly `Terraria.WorldBuilding.StructureMap`, and
`AddProtectedStructure(Rectangle, int padding)` is one public call. Calamity registers
every lab and shrine this way the moment it pastes them, with a padding of 4 for shrines
and 20 for labs.

**It is worth doing and it is not enough.** Nine of vanilla's hundred-odd passes consult
it. These do:

| pass | what it would otherwise drop on you |
|---|---|
| Micro Biomes | cabins, campsites, enchanted swords, honey patches, mining explosives, corruption pits, mahogany trees |
| Dunes · Full Desert | sand |
| Marble · Granite | a cave through the middle |
| Hives | a hive |
| Shimmer | the pool — which is the one we *want* placed, so protect Grace's core only after it lands |
| Jungle Chests · Buried Chests | a chest inside a wall |

Everything else ignores it. Tunnels, Mount Caves, Dirt Layer Caves, Rock Layer Caves,
Surface Caves, Wavy Caves, Small Holes, Lakes, Silt, Shinies, Spider Caves, Gem Caves,
Webs, Moss, Cave Walls, Stalac, and `WorldGen.TileRunner` in general will all run a hole
straight through a protected rectangle without asking. Those are exactly the ones that
would ruin the Ball and the Undercity.

So protection is three things, not one.

### 1 · Register

One line per structure, immediately after placing it, with generous padding:

```
GenVars.structures.AddProtectedStructure(rect, 20);
```

For Grace: the capital, the Curtain, the Undercity, the Reliquary and the Rootworks each
get their own rectangle rather than one enormous one, so the padding hugs the shapes.
For the Ball: the shell's bounding box, and **not** the arms — the arms are supposed to
be tangled up in whatever is around them.

### 2 · Go last

Registration cannot stop the passes that never look, so the answer to those is to run
after them. Both of the big set pieces are already scheduled that way in §5: the Ball
after `Corruption`, Grace after `Micro Biomes`. That wants tightening to the real
constraint — **after `Gem Caves`, which is the last pass that carves caverns at scale.**

Anything that runs after us and could still damage us — `Moss`, `Stalac`, `Water Plants`,
`Tile Cleanup`, `Final Cleanup` — is cosmetic. It grows on our tiles rather than removing
them, and the Grace Cleanup pass at the end of the list is where anything it got wrong
gets repaired.

### 3 · Leave the edges to the world

Calamity's real trick, and the one that costs nothing to copy. Their schematic format
registers a tile named `"_"` at index 0 of the mod-name arrays; a cell marked with it
means *leave whatever was already here*, and on paste the original tile is handed back to
the cell so it restores.

The point is that a building has two kinds of tile: the ones that were designed, and the
ones where it meets the ground. Authoring the second kind is what makes a structure look
stamped on.

For us that means the authored parts of Grace — the Court, the granaries, the
Processional — are drawn tile by tile, and every cell around their footings is a
**keep** rather than a tile. The Curtain's foundation blends into whatever the plain
turned out to be. The Ball's shell writes only where it is solid and leaves the rock it
meets alone, which is also why the arms can wander into a spider cave and look like they
belong there.

### What still needs checking

- The Ball is fourteen hundred tiles across with its arms out and sits under the beach at
  x 250 – 1650. Even going last, it has to lose gracefully where it meets the Dungeon at
  1400 – 1750 — that is what the 60-tile keep-out in §4 is for, and it wants testing.
- `structures.Reset()` runs inside `GenerateWorld_RunTasksAndFinish`, so every
  registration is per-generation and none of it survives into the world file. Nothing
  protects Grace from the player, which is correct.

---

## 5b. The two biomes that place themselves

`Reset` decides where the Dungeon, the snow and the jungle go, so those three are simply
overwritten. **The desert and the primary evil are not decided there.** Each picks its own
x during its own pass and then spends a while rejecting the pick against whatever else is
already in the world — which in a world half again as wide as anything vanilla has seen
means either of them can land in the Wasted Mile or under the capital. In the first
generation off this plan the desert came out at the Grace end.

Neither can be told where to go. Both can be told **where not to**, and the fields they
consult are ordinary statics. So each vanilla pass is wrapped in a `BracketPass` that sets
those fields on the way in and puts them back on the way out. Nothing is rewritten, the
pass keeps its own name and weight in the list, and nothing else in the world ever sees
the lie.

| biome | what it consults | what it is handed |
|---|---|---|
| **Dunes** — the loose sand and the pyramids, `DunesBiome`, *not* the desert | `WorldGen.RandomWorldPoint`, then rejects against the jungle (±1800), the middle of the world (±300) and the snow (±300) | a point already inside x 3400 – 4700. All three tests pass on the first roll. Three other passes call `RandomWorldPoint` and are left alone — the bracket is what says which call this is. |
| **Full Desert** — the desert itself, surface and underground, `DesertBiome` | nothing. It rolls `x = maxTilesX / 2 − dungeonSide × offset`, `offset ≥ maxTilesX / 16`, and retries on the terrain alone | a rewritten origin. `On_DesertBiome.Place` puts x inside the band on every roll; the pass keeps its own retry loop, so bad ground just gets another x in the band. |
| **Corruption** | rolls `genRand.Next(500, maxTilesX − 500)` once, then argues with the answer — the Dungeon, the middle of the world, the underground desert, the snow and the jungle can each throw it away | a rewritten roll. `On_UnifiedRandom.Next_int_int` returns an x inside the band for that one argument pair; every rejection the pass makes still applies, so the evil keeps out of the desert next door on its own. |

**Do not steer a biome with vanilla's avoidance fields.** The first version of the
Corruption bracket set `GenVars.evilBiomeBeachAvoidance` to the band's near edge, on the
grounds that the pass clamps its own ends against it. It crashed vanilla, and how is worth
keeping written down: those two clamps are **symmetric** —

    left  = max(left,  avoid)
    right = min(right, maxTilesX − avoid)

— so a large avoidance and a roll near the far end of the world come out with **left
greater than right**. Nothing downstream checks for that. Forty lines later the pass hands
the pair to `Next(min, max)` and throws `ArgumentOutOfRangeException` from inside
`b__288_28`, with a PisAndDag frame in the trace only because our bracket is on the stack.
The roll is the lever; the clamps are not.

**The desert was the one that had to be found the hard way.** Dunes is named after sand and
places sand, so it looks like the desert pass and is not: `DesertBiome` belongs to **Full
Desert**, twenty-odd passes later, and vanilla's rule for it is *the side of the world
opposite the Dungeon*. In this layout the Dungeon is at the ocean end and so is the
desert, which means vanilla's own rule threw the desert across the world onto the jungle
and the snow every single time.

`DesertBiome.Place` takes a `StructureMap` and only ever **adds** to it — it never asks
`CanPlace` — so a wall built in that map does nothing, and `GenVars.dungeonSide` only
moves the roll into the right half of the world rather than into the band. The origin
itself is the only honest lever, and that is what the hook rewrites.

All of them are restored in a `finally`, so a throw inside a vanilla pass cannot leave the
world with a lie in `GenVars`.

### Shell Piles walks off the end of the world

The same bracket carries one fix that is not about bands. **Shell Piles** reads the two
marks the Beaches pass left on the sand — `GenVars.shellStartXLeft/YLeft` and
`…Right` — scans twenty tiles either side of each and ten above, and then walks up to a
hundred and twenty-five tiles further out to drop the piles.

On the Grace side there is no ocean, only the sixty-tile sliver, so the mark sits at the
very edge of the world and that scan reads straight through the end of the tile array:
`IndexOutOfRangeException` in `b__288_51`, in vanilla, with no mod named in the trace.
Vanilla only walks each beach **on a coin flip**, which is why it took several worlds to
show itself.

Nothing in the game reads those four fields except this pass, so they are simply clamped
to 170 tiles from either edge and 70 from the top before it runs. Shells that then land on
the sliver are buried by the Grace Plain twelve passes later without being asked.

### Nothing giant grows on the pitch or the field

A living tree is a hundred tiles of trunk and root and it does not ask what was planned
for the ground it lands on. `On_WorldGen.GrowLivingTree` (the Living Trees pass) and
`On_WorldGen.GrowEpicTree` (Planting Trees) both refuse inside the Grace Plain and the
Wasted Mile. The cleanup pass at the end still strips ordinary growth; this is for the
ones too big to strip cleanly.

On the Cleared Ground a giant may still grow. It is cut down there (§5c).

The graveyard is the same problem in a different pass. It picks a beach at random and
searches inland from it — and the Grace end has a sixty-tile sliver of beach with the
capital's ground behind it. In a Grand world it now takes the ocean side, always, and
never falls back to the other one.

---

## 5c. The Cleared Ground keeps its ground and loses its islands

§3 says the Cleared Ground is empty. Vanilla does not leave it empty, and most of what it
leaves is wanted: the lumps of dirt, the ponds and the cracks are the character of the
place, and a first make that levelled them away was turned down for it. **PAD Cleared
Ground** (`ClearedGroundPass`) levels nothing. It does two things.

- **The floating islands go.** Vanilla's Tunnels pass hangs a crust of soil a dozen tiles
  over the ground on nothing, and the room under it comes out hung with leaf wall. A crust
  is floating land when its top stands five rows or more over the ground either side of it;
  a cave's roof lies level with that ground and is left. An island is taken off down to the
  ground under it, with the walls that stood under it, and one that starts on the Cleared
  Ground is followed off either end of it. The ground it stood over, and any water lying
  there, is not touched.
- **A living tree on it is a stump**, five blocks over the ground beside it, broken at the
  top, with its foot, its roots, its hollow and the room under it kept. One standing past
  either end is left whole.

There is no made flat for 'Dame' Quilt: nothing is levelled for her. She is put down where
the ground vanilla left is flattest (`DonaQuinnEncounter`: the column with the most flat ground
on its shorter side, flat being ground that keeps within six rows and steps two at most, with a
living tree's stump passed over), and she comes the first time a player stands on the Cleared
Ground within 1350 pixels of that, which is off their screen.

Her fight is boxed in (`BossArena`): two walls nobody sees, always half the Cleared Ground
apart (445 tiles), their middle halfway between the player and where she was put down. The
player cannot walk past one, their camera does not show past one, and inside them nothing is
built, broken or blown up (the Boss Active buff). The box stands from the road until she is
thrown; she herself is not held by it.

Numbers and how it was checked: `PADWorkFlow_1.md` §16.24 for the pass, §16.25 for the flattest
place and the box.

---

## 6. What the player's first hour looks like

Spawn at 6300, in forest, and the two directions are not the same kind of country.

- **East is a queue.** Two hundred tiles and the ground turns to snow — close enough that
  the player can see the line from where they woke up. Past the snow, a thin band of
  forest, then jungle. Nothing out there is optional and nothing is far apart, until the
  jungle ends and the Cleared Ground starts and there is suddenly a very long way to
  look.
- **West is a country.** Desert at 3400, a stretch of something wrong at 2400, the
  Dungeon, and four hundred tiles of ocean, with real distance between them. Nothing on
  that walk hints that the other evil is sitting two thousand tiles under the sand.
- And on a clear day, east, above everything: **the Curtain**, 160 tiles of pale stone
  standing over the horizon at 10800, visible from the far side of the jungle and not
  reachable for a long while.

That silhouette is the whole reason the plain is 1800 tiles wide. It has to be seen from
far enough away to become a plan.

---

## 7. Risks

1. **Grace is walkable on day one.** Gating is internal and physical
   (`PAD_Grace_Structure_Plan.md` §6). It needs to hold against a player with a
   grappling hook and no patience.
2. **The Ball with its arms out is fourteen hundred tiles wide.** At x ~250 – 1650 it
   sits under the whole beach and will intersect whatever the cavern passes put there —
   spider caves, gem caves, marble, ordinary tunnels. It has to be carved last among those
   and it has to win, and the two hard limits in §4 (nothing above y 1500, nothing within
   60 of the Dungeon) are what keep it from eating the Dungeon's lower reaches.
3. **Twelve orbs in one room is four boss summons and four times the meteor pressure.**
   The depth is the gate and it may not be gate enough. Needs playing.
4. **The Sealed Pocket at hardmode** puts a third infection front in the middle of the
   map. Pre-hardmode it is inert; the moment the Wall dies it is not, and it is nowhere
   near the other two — the two evils are both west of 3400, and the Hallow now comes up
   at 9900, on the Cleared Ground, directly in front of Grace. The absolute of perfection
   arrives at the doorstep of the kingdom whose whole claim was favour. That is the
   intended reading, and it does mean the Hallow will eat into the capital.
5. **Snow two hundred tiles from spawn** is a harder first ten minutes than vanilla gives
   anybody: ice slimes and slower ground before the player has boots. Intended, but it
   wants a check that a fresh character can survive walking east by accident.
6. **The Mors monument is built around `Main.spawnTileX`.** Its radius has to clear the
   Snow's left edge at 6500, or the monument generates half in the ice.
7. **The plain is 1800 tiles of nothing but Grace.** If Grace does not fill it, it reads
   as a bug. Either the capital grows or the plain shrinks.
8. **Multiplayer.** All of §5 is world generation and travels in the world file. Nothing
   here is client-side.

---

## 8. Open

- The Ward Gate key (`PAD_Grace_Structure_Plan.md` §6).
- Whether the Wasted Mile's bore is still working because it was left running or because
  something is running it.
- Where the sea the Dry Shore was built for went.
- Whether the Sealed Pocket's gallery was cut by the houses or by a later visitor, and
  whether the Ball's shaft was cut by the same one.
- Who set the twelve out, and whether twelve is a count or a ration.
