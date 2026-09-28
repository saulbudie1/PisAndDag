# Before release

Things in the tree that are for testing, or unfinished, or should not ship. Tick as you go.

## Test binds and commands — remove

- [ ] **Wind test.** `WindTest.cs` (whole file: `WindTest` + `/wind`). `PADKeybinds.Wind` and its `Load`/`Unload` lines. In `PisPlayer.ProcessTriggers` the `PADKeybinds.Wind` guard and `StepWind()`. `Keybinds.Wind.DisplayName` in the hjson.
- [ ] **Lightning bind (P).** `PADKeybinds.Lightning` and its lines; `PisPlayer.ProcessTriggers` guard and `ThrowLightning()`; `Keybinds.Lightning.DisplayName` in the hjson. The lightning *effect* and Rage Of A Thunder stay; only the free bind goes.
- [ ] **`/mors`** — `MorsCommand.cs`. Includes the `again` reset and the graveyard diagnostic line. Mors spawns himself (`MorsPresence`); the command is only for placing him.
- [ ] **`/megashark`** — `MegasharkCommand.cs`. The real summon is the Minishark (`MegasharkCall.cs`).
- [ ] **`/legends`** — `LegendsCommand.cs`. Legends is switched on by the Orb.
- [ ] **`/struct`** — `StructCommand.cs`. Worldgen structures place themselves.
- [ ] **`/magicarsenal`** — `MagicArsenalCommand.cs`, plus `MagicArsenal.Maxed`, `Max()`, `Normal()` and the `"maxed"` save key. Slots should only open by being earned.
- [ ] `Description` strings on any command that survives should not say "for testing".

## Should not be packed — `build.txt`

Add a `buildIgnore` so these stay in the repo but out of the `.tmod`:

```
buildIgnore = Claude outputs\*, Reference\*, *.md, *.py, *.aseprite, *.fx, Effects\Compiler\*, .git\*, .vs\*
```

Then check the built size dropped.

- [ ] `Claude outputs/` (previews, PDFs, reference gifs)
- [ ] `spritecheck.py` in the root
- [ ] `PADWorkFlow_1.md`, `PAD_Lore_Planning.md`, `Terraria_Mod_Lore_Bible_ver_1.1.md`, this file
- [ ] `Content/NPCs/Mors/RoughIdea.png`, `Mors_LitPass.png`, `Sprite-0004.aseprite` — sources, not sprites the game loads
- [ ] `Effects/*.fx` and `Effects/Compiler/` — only the `.xnb` is read
- [ ] `.gitignore`: the `*.exe` / `*.msi` lines, so an installer never lands in a commit again

## Provisional — decide, then either finish or cut

- [ ] **Megashark summon** — 20 Minishark rounds into the ocean after the three mechs. Placeholder until a proper summon is designed. Boss Checklist's spawn-info line (`BossLog.Megashark.SpawnInfo`) must match whatever it ends up being.
- [ ] **`MorsPresence.Stage`** — only stage 0 exists (graveyard). Nothing raises it yet. Either the next milestone lands or the field is dead weight.
- [ ] **Mors dialogue** — two answers and four openers. Fine for a first meeting; thin for a second. Later stages need lines.
- [ ] **Mors head-only turn / body toward graveyard** — the "player choice flags" that were going to change his body facing don't exist yet.
- [ ] **Graveyard line once per world** — check it does the right thing in multiplayer (first player to talk takes it).
- [ ] **Worm crawl during the plate** (`BossArrivalWait`, 2.6 px/tick) — watched once with the Eater. Confirm with the Destroyer, which spawns deeper.
- [ ] **Event bosses as plain** (`BossPlate.NotBosses`) — list was built from vanilla's boss-head set; check Old One's Army in play, and whether Betsy should be a boss after all.
- [ ] **Lore Bible: "nine hundred years"** — 5 in the Bible, 3 in the chronicle. You said ~300 and no number. Strip them; keep the figure in `PAD_Lore_Planning.md` only.
- [ ] **White Death** — still a registered boss (`Content/NPCs/WhiteDeath`). Is it shipping?
- [ ] **Aquatic Firearms** — no source. Red rarity, so it belongs late.
- [ ] **Reliquary Key** — no source.
- [ ] **Arsenal slots** — nothing raises them past 3 (the memory consumable, Item Plan §4.6 step 6, is not built).
- [ ] **Incantations before Skeletron** — the only seal comes after him, but Meagre Flame drops earlier.

## Known rough edges

- [ ] `QuickSpawnClonedItem` obsolete warnings.
- [ ] `AshDissolve.cs:75` fade bug.
- [ ] `Content/Projectiles/Ranged/ChlorophyteBullet.cs` / `.png` — was marked for deletion.
- [ ] `Content/NPCs/Legends/SlimeMass.cs`, `Content/Sounds/AbomanationWarn.ogg`, the `Content/Tiles/SealGlass.cs` stub — to delete.
- [ ] `AquaticFirearmsBattery.cs` doc comments still say damage ×2/3; the code is gun + ball.
- [ ] Placeholder art — every row in `PlaceholderTextures.md`.
- [ ] `description.txt` is still the template placeholder. `build.txt` version is `1.0` — set it honestly.

## Last

- [ ] Recompile every `.fx` after any shader edit; a stale `.xnb` throws on `Parameters[...]` at draw time, not at load.
- [ ] Fresh world, fresh character, no commands: die once, walk to the graveyard, talk to him.
