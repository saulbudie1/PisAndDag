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
- [ ] **`/dona`** — `DonaQuinnCommand.cs`, plus `DonaQuinnEncounter.Summon`, `Reset` and `Describe`. She comes by herself, once a world, on the Cleared Ground.
- [ ] **`/guideintro`** — `GuideIntroCommand.cs`, plus `GuideIntro.Again` and `Describe`. The Guide's first words play by themselves, once, in a world made with them.
- [ ] **`/abomanation`** — `AbomanationCommand.cs` (the call, and the words that make it do one move next: rush, lunge, puke, ash, vent, spasm, whip), plus `AbomanationHead.Force`, `forcedMove` and `forcedRush`. It is called by letting ten animals go on the Mile.
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
- [ ] **'Dame' Quilt's numbers** — Triunfo jumps 10 tiles and leaps 14; a fetch gives up after 8 s (15 s without the lance at all) and the lance is in her hand regardless; 35% swing, 30% throw after a charge. Mine, not asked for: confirm in play.
- [x] **The Guide's hut line** — `Hints.GuideHut`, said in his first words too, sends the player "seawards", but the Blood Shrine was built at the nearer end of the spawn's forest: in a Grand world the snow side, with the sea the other way (and the Piece of Paper says so). The shrine now goes on the sea's side (PADWorkFlow §16.21). Worlds made before keep it where it was.
- [ ] **The Guide's first words: numbers** — put down 15 tiles off, stops 3.5 blocks away, walks at 1.4 px/tick, "..." to move each line on. Mine, not asked for: confirm in play.
- [ ] **Amalgaermis's moves: numbers** — all mine but the ones asked for (5 to 8 bones, 2 skulls of 3 bones, 2 hands, 0.7 s then 0.3 s between heaves, 500 back on expert Legends). A move every 4.5 to 6.5 s of chase (3 to 4.5 in the second half); rush 5, puke 3, ash 3 by weight; 68% of rushes through you; a spasm every 5 to 8 s, 1.2 to 1.8 s long; it lies down 26 tiles off and stops 12 short; what things hit for (bone 26/44/66, skull 32/56/84, hand 30/52/78, ash 22/40/60, steam 26/46/68). All in `Abomanation.cs`, each with what it does. Confirm in play (PADWorkFlow §16.23).
- [ ] **Amalgaermis: added without being asked** — the vent's steam scalds (`Abomanation.VentScalds = false` turns it off); a hand's burst hits within 52 px; the whip may follow any move, not only a rush; the rush's tell is its bark, not vanilla's roar; the puke and lying down wait for the head to be under the ground, so they come up to 4 s late. Keep or cut each.
- [ ] **Amalgaermis: the puke is slower to arrive than it could be** — about 2.5 s from the move beginning to the tell, most of it the body following the head under the ground (so no tail is left hanging in the air). If the fight drags, shorten `MoveGateLeast`/`Most`, not that.

## Known rough edges

- [ ] `QuickSpawnClonedItem` obsolete warnings.
- [ ] `AshDissolve.cs:75` fade bug.
- [ ] `Content/Projectiles/Ranged/ChlorophyteBullet.cs` / `.png` — was marked for deletion.
- [ ] `Content/NPCs/Legends/SlimeMass.cs`, `Content/Sounds/AbomanationWarn.ogg`, the `Content/Tiles/SealGlass.cs` stub — to delete.
- [ ] `AquaticFirearmsBattery.cs` doc comments still say damage ×2/3; the code is gun + ball.
- [ ] Placeholder art — every row in `PlaceholderTextures.md`.
- [ ] `description.txt` is still the template placeholder. `build.txt` version is `1.0` — set it honestly.
- [ ] **'Dame' Quilt's bar after her fall** — `DonaQuinn.cs` sets `NPC.boss = fighting`, meaning the bar to go at the fall, but nothing that draws the bar reads that flag: vanilla tracks any active NPC with a boss head, and `BossPlate.Track` holds the bar while she is active with life over 0. Read off the code, not seen in play: check whether an empty bar stays up through the plea and the talk (PADWorkFlow §16.22).
- [ ] **The Megashark's other drops** — its comment says the kill is worth one gun and nothing else, but it is a boss with no `BossLoot` override, so vanilla adds 5 to 15 Lesser Healing Potions and 5 to 9 hearts. Keep them or cut them (PADWorkFlow §16.22).
- [ ] **Amalgaermis's voice has not been heard** — `Content/Sounds/Abomanation*.ogg`, 20 files besides the screech, made by synthesis and only measured (level, where their energy sits). Listen to each; any that is wrong can be remade.
- [ ] **Amalgaermis's old rush misses** — the rush that climbs over you passes a player standing still about nine tiles to one side (measured in the simulator; it always did). Kept as the rarer of the two. Fix it, keep it as a feint, or cut it (`Abomanation.ThroughChance`).
- [ ] **`Content/Particles/DustFog.png` is not premultiplied** — white with the shape only in its alpha, drawn in the premultiplied pass, so the colour does not fall away with the alpha. King Slime's slam, the Queen Slime, Cold Call and now Amalgaermis's ground all use it as it is. Premultiplying the file changes all of them at once: look before and after.
- [ ] `PieceOfPaper.cs`: the comment over the class describes the Grand map as it was before you rewrote its lines (it still names the graveyard, and quotes "the vast jungle and iceland").

## Last

- [ ] Recompile every `.fx` after any shader edit; a stale `.xnb` throws on `Parameters[...]` at draw time, not at load.
- [ ] Fresh world, fresh character, no commands: die once, walk to the graveyard, talk to him.
- [ ] Amalgaermis: `/abomanation`, then each of `rush`, `lunge`, `puke`, `ash`, `vent`, `spasm`, `whip` (the vent gives it nothing back until it is under 40%). Then a whole fight with no commands, and one with a second player.
