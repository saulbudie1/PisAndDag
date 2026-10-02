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
- [ ] **The chopped forest: numbers** — the 5 blocks of a living tree's stump are yours; the rest are mine. Floating land is a crust 12 thick or less over 3 or more rows of room, with its top 5 or more rows over the ground either side of it; it is followed 120 columns past either end of the Cleared Ground; the cut moves up to 2 rows to sit on the tree's own top, and is measured from the higher of the ground on the trunk's two sides; splinters up to 4 blocks, heartwood up to 6. All in `ClearedGroundPass.cs`, each with what it does. Run on Far Tract of Potions' terrain outside the game, never seen in a generated world (PADWorkFlow §16.24).
- [ ] **The chopped forest: added without being asked** — the Mile's four signs now stand on `Layout.Ground` (in Far Tract of Potions three of them are on the sky island over the way in); `GraceCleanupPass` no longer empties the inside of a living tree (it was taking the platforms, the doors, the chest and the loom from the room under it). Keep or cut each.
- [ ] **The shrine's chest: the six other things** — the Orb, the 5 Life Crystals, the seven and the 20 to 25 bones are yours. What the other six are picked from is mine, eighteen things each with its own stack (`PADStructureSystem.ShrineSpare`): torches, rope, glowsticks, five kinds of potion, musket balls, arrows, throwing knives, shuriken, bombs, iron and lead bars, silver, cobweb, candles. Change the list to taste.
- [ ] **'Dame' Quilt's box: numbers** — yours: always half the Cleared Ground wide (445 tiles), its middle halfway between the player and where she is put down, she is put down where the ground is flattest, and Boss Active only in her box. Mine: she comes when a player is within 1350 px of that place (it used to be thirty tiles onto the Cleared Ground, from either end); flat ground keeps within 6 rows, steps 2 at most, has 3 rows of air over it and no water, a living tree is passed over for 40 columns, and the place is kept 114 tiles in from either end; she is put down within 6 tiles of where she was meant to be; the camera settles against a wall, and comes away, over 40 ticks; a player more than 320 px past a wall is let go. Confirm in play (PADWorkFlow §16.25).
- [ ] **'Dame' Quilt's box: added without being asked** — it has no top and no bottom (two walls); it holds any player who is inside it or walks in, not only the one she stopped; nothing inside it can be blown up (`BossArenaTiles`, `BossArenaWalls`: the buff cannot stop that itself); `/dona` says where the flattest place is and `/dona reset` forgets it. Keep or cut each.
- [ ] **'Dame' Quilt is not held by her own box** — nothing in her fight was changed, so a charge still rides on 300 px past the player: with the player's back to a wall she leaves their screen for about two seconds and comes back. The other choice is that she pulls up at the wall, and then a player with their back to one is out of a charge's reach. Decide in play.
- [ ] **Where the box falls** — its middle is halfway between the player and her, as asked, so it is not kept inside the Cleared Ground: in Far Tract of Potions, coming from the jungle, it runs x 9303 to 9748 and 137 of its 445 tiles are before the Cleared Ground's start, because the flat ground is at that end. Keeping it inside would move its middle off that halfway point: say if it should.

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
- [ ] **The cleanup strips a long way down** — `GraceCleanupPass.Strip` takes every framed tile from `worldSurface - 400` to `worldSurface + 40`, and under the Cleared Ground, the Mile and the plain the surface is about 150 rows over that line. In Far Tract of Potions there is no pot, chest, heart or stalactite between y 700 and 845 under the Cleared Ground, and 590 pots and 4 hearts in the 150 rows below. Meant, or should its reach follow the ground?
- [ ] **The snow is not where `Layout` puts it** — in Far Tract of Potions the surface snow runs x 6332 to 7306, 29 tiles from spawn; `SnowStart` and `SnowEnd` say 6825 to 7475. Seen while testing, not looked into.
- [ ] **Boss Active does not stop everything** — it stops blocks and walls being placed, the pick, the axe, the hammer, the shovel, buckets and the drill (vanilla's `noBuilding`), and the box stops explosions. Not stopped: a tile put down by a projectile (a dirt bomb, the Sandgun), liquid from a bomb or a rocket, wire, paint, a weapon cutting grass or breaking pots. Vanilla has no one place those go through (PADWorkFlow §16.25).
- [ ] **The flattest place, in a world made before the islands came off** — it is looked for from the ground under the first player to stand on the Cleared Ground. In a world that still has its floating islands (Far Tract of Potions), a player standing on one of them starts the walk up there, and the place found is another, shorter one. `/dona` says where it is; `/dona reset` forgets it. New worlds have no islands there.

## Last

- [ ] Recompile every `.fx` after any shader edit; a stale `.xnb` throws on `Parameters[...]` at draw time, not at load.
- [ ] Fresh world, fresh character, no commands: die once, walk to the graveyard, talk to him.
- [ ] Amalgaermis: `/abomanation`, then each of `rush`, `lunge`, `puke`, `ash`, `vent`, `spasm`, `whip` (the vent gives it nothing back until it is under 40%). Then a whole fight with no commands, and one with a second player.
- [ ] A new Grand world, on foot from the jungle to the Mile: no land floating over the Cleared Ground and no leaf wall standing on it; its lumps, ponds and cracks where vanilla put them; every tree on it a stump; the four signs on the ground before the ash. If a living tree landed on it: its stump, and the room under it with its chest. Then the Blood Shrine's chest: the Orb, 5 Life Crystals, 20 to 25 bones and six other things.
- [ ] 'Dame' Quilt's box. `/dona` for where the flattest place is; walk at it across the Cleared Ground (or `/dona summon` within eighty tiles of it): she is put down on it, off the screen. Boss Active is on the buff bar from then until she is thrown. Run at each wall: the camera stops, then you do. Inside it, try a pickaxe, a block and a bomb. Let the swing throw you at a wall. Take her to one life with your back to a wall and watch the camera come away as she falls. Then with a second player who starts outside the box and walks in.
