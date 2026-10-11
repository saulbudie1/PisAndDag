# PisAndDag — NPC Plan

> Every NPC and boss the mod adds, and what each one is supposed to DO.
> Lore lives in `Terraria_Mod_Lore_Bible_ver_1.1.md`; this file is the machine, the same
> way `PAD_Item_Plan.md` is the machine for items.
>
> `build.txt` has no `modReferences`. Behaviour here is modelled on Calamity in places and
> balanced against **vanilla**. Nothing may depend on Calamity loading.
>
> Numbers marked **measured** came out of a simulation of the real code against a moving
> player, not out of anyone's head. Where a number is a guess it says so.

---

# 1 · Roster

| npc | folder | state |
|---|---|---|
| Megashark | `Content/NPCs/MegaShark` | built, three phases + enrage; in Boss Checklist |
| White Death | `Content/NPCs/WhiteDeath` | stub, one attack, unfinished |
| Mors | `Content/NPCs/Mors` | built, town-side, dialogue |
| Legends slimes / Guide sacrifice | `Content/NPCs/Legends` | built |
| **Abomanation** | `Content/NPCs/Abomanation_Small` | **built; one move planned, §2.4** |
| **Foigh, the Swordsman** | `Content/NPCs/Foigh` | **his road, phase one of three and the passage into phase two built; put down by `/foigh` only; §5** |

---

# 2 · Abomanation

A worm that lives in the sand and comes up through it. Desert, pre-hardmode.
Head steers and owns the health pool, fifteen body segments follow it and carry the arms,
the tail closes the chain and is bare.

HP 6000 / 9000 / 12000. Contact only, so far.

## 2.1 What is built

| | |
|---|---|
| `Abomanation.cs` | every tuning number, the arm renderer, the follow chain |
| `AbomanationHead.cs` | steering, the phase machine, spawn layout |
| `AbomanationBody.cs` | follow + draw, five sprites cycling, mirrored on a coin |
| `AbomanationTail.cs` | follow + draw, nothing else |
| `AbomanationCommand.cs` | `/abomanation`, test spawner, mod root |

**The six knobs** are `Fly`, `Speed`, `TurnSpeed`, `Stray`, `RushSpeed`, `Distance`.
`TurnSpeed` is **radians per tick**, not an acceleration; the turning circle is
`Speed / TurnSpeed`, so the two have to be scaled together. Both facts are written at
length in the file and are easy to get wrong.

Currently **Speed 6.5** (24 tiles/s, just above Hermes Boots) and **TurnSpeed 0.0275**,
which is a **15 tile** turning circle and a U turn taking just under two seconds.

## 2.2 Current fight

Two beats.

- **Chase.** Angular steering plus a wandering offset, turn capped, so every reversal is a
  U turn rather than a stop. It does **not** aim at you the whole way in: inside
  `PassTrigger` (300px) it locks a point 8 – 20 tiles on the FAR side of you, down the line
  it is already travelling, and holds that until it arrives. **Measured:** that takes it
  from 28 passes in two and a half minutes to 17, and from 1179px at its furthest to 1445.
  Fewer, longer runs with a real turn at each end, instead of a hover.
- **The rush.** Ten seconds of chasing from below, then a dive to 800px under the player,
  a banked turn, and a climb of `Distance` tiles straight up through the ground they are
  standing on. **Measured:** tops out ~169px over the player's head.
- **In the air.** Outside the rush, if the head leaves the ground and its aim is within
  `AirSwimDistance` it stops steering altogether: `velocity.Y += AirGravity` (0.2) and
  nothing else. No drag, no cap, no floor. It keeps whatever momentum it left the ground
  with, arcs, and comes down where that arc lands; the U turn happens underground after.
  Phases 2 and 3 (the rush and the fall over the top) are exempt, and so is `Fly`.

**ExampleWorm is a reference, not the model.** Its `WormHead` steers by accelerating each
axis toward a target velocity, which is vanilla worm AI and is exactly what made this stop
dead and turn inside out when you got behind it. The pass-by idea is worth having; the
steering underneath it is not.

**The gap this leaves.** Contact damage on a worm only ever means "do not be where it is".
There is no area denial and nothing that punishes holding a position, so a platform setup
trivialises it. §2.4 is the answer to that and the only reason it exists.

## 2.3 Phases in `AbomanationHead`

`phase` is 0 chase, 1 dive, 2 rush, 3 fall. The planted move adds 4, 5, 6 — see below.
Everything is synced by hand in `SendExtraAI`; a client that guessed at the attack state
would send the body somewhere the server never went.

---

## 2.4 The Implanting — **PLANNED, NOT BUILT**

> Very rare. The animal stands itself in the ground, points its back end at the sky and
> empties itself over the area. It is not aiming at anybody.

### The shape of it

1. **Approach.** Head climbs to a mark beside the player and turns over.
2. **Implant.** Head drives straight down through the mark, **bleeding speed off as it
   goes**, so the body threads into the hole and settles rather than stopping dead.
3. **Spray.** Head holds still 38 tiles under the sand. The tail is now planted at the
   surface, stump in the air, and sprays for **five seconds from the first shot**.
4. **Shake.** The tail shakes itself off, about a second, decaying.
5. **Leave.** Head drives away and the fight resumes.

### Why the tail cannot aim, and what that forces

The tail is the end of a rigid sixteen link chain that only ever follows. It has no
steering of its own and never will. So the head has to drive a path whose **last 678px is
vertical**, because the tail sits exactly one body length behind along whatever path the
head actually took.

Three ways of doing that were simulated and two were wrong. Recorded so nobody tries them
again:

| approach | result |
|---|---|
| drive the head to a fixed depth | column **155px off vertical**, tail ends **13px under** the sand |
| descend until the tail passes the mark | tail buried **879px deep** — it never stops while the head moves |
| **head to exactly one body length below the mark, from 900px above it** | **works** |

The 900px of clearance is not arbitrary: the turn to vertical costs 257px of travel at the
current `TurnSpeed`, and the body is 678px. Anything less and the column arrives bent.

**Measured, at the current constants:**

| | |
|---|---|
| tail centre lands | **4px** from the mark |
| stump clears the sand by | **101px** |
| column lean | **98px** over 678, about 8° |
| head ends up | **38 tiles** down |

The lean is an accident worth keeping. A ruler straight post looks worse.

### The spray

**No targeting.** A sprinkler around the vertical: angle `-PI/2 ± 0.95` rad, speed 7 – 13.5,
gravity 0.26, three gobs every 13 ticks. It does not care where the player is, which is the
point — it denies the **ground**, not the player.

Each gob arcs, lands, and leaves a lingering pool that poisons on contact.
**Measured: 49 pools** on the ground by the end of one spray, spread either side of the
plant point.

Trails: `ProjectileID.Sets.TrailCacheLength` + `TrailingMode`, walked in `PreDraw` off
`oldPos`. The preview fakes this with a position list; the real one does not need to.

### Timing — measured

| stage | ticks | seconds |
|---|---|---|
| approach | 182 | 3.03 |
| implant | 246 | 4.10 |
| **spray** | **301** | **5.02** |
| shake | 55 | 0.92 |
| leave | 72 | 1.20 |
| **whole move** | **856** | **14.3** |

**14.3 seconds is a long time to hand a boss**, and about six of those are the head
travelling underground where very little is visible. That is the cost, and it is the reason
this has to be rare rather than a rotation slot.

### How rare

Not on the rush's timer. Roll after each completed rush.

- 1 in 5 → about one every 80s of fighting, plus the 14.3s of the move itself.
- **1 in 8 → about one every two minutes.** Against 6000 HP that is once or twice in a
  fight, which is what "very rare" should mean here. **Start here.**

Keep a minimum gap so two cannot land back to back.

### Damage window

`realLife` routes all damage to the head, so a stationary planted worm is a large free DPS
window for the player. That is the trade and it is a fair one: it takes the ground away
from you, you get to hit it for five seconds.

### Still open

- Does the plant point sit a fixed distance to the side (304px / 19 tiles in the preview),
  or land wherever the head happens to be when the roll comes up? The second is less work
  and less predictable; the first keeps the stump on screen.
- Poison: vanilla `BuffID.Poisoned` is weak (−1 hp/s, no regen), so the threat has to be
  the pools' contact damage, not the debuff. A debuff of the mod's own would need a name
  and a line in the Bible.
- Whether the **descending column** should hurt. It passes through the player's level at
  the plant point and currently would, by contact.
- Gob, pool and splash sprites: none exist.

### Files it would need

| | |
|---|---|
| `AbomanationGob.cs` + `.png` | the arcing glob, spawns a pool on tile hit |
| `AbomanationPool.cs` + `.png` | the lingering patch, damage + poison on contact |
| head | phases 4 / 5 / 6, the roll, the plant mark |
| tail | read the head's phase, fire, and carry the shake wobble on its rotation |

---

## 2.6 The Whip — built

> Occasional. It comes for you at its ordinary pace until it is close, turns once in a
> tight loop so the body coils up behind it, then drives straight out of the coil. The
> body comes after it with all the turn still in it and the tail leaves the end of the
> animal in one wide arc. Then it swings back, less, and lies down.

Phases 4 (run in), 5 (loop), 6 (crack). Rolled after a finished rush: 1 in 3 with a 20 s
gap in the first half, every rush with a 4 s gap in the second.

**Nothing in the lash is scripted.** During the crack each body link is a damped spring
in ANGLE toward the link ahead — stiffer toward the tail, turning speed capped
(`AbomanationChain.Swing`). The coil, the wave down the body, the arc and the recoil all
fall out of the head's two moves and that one rule. The head's loop is roomy (radius 100)
on purpose: the follow chain cuts corners, and a tighter loop leaves the body straight
with a kink at the neck instead of a coil you can see.

**Measured** (head 9 px/t round 100, then 10 px/t straight; springs 0.05 → 0.38, damping
0.90 falling to 0.60 from tick 25, turn cap 0.16 rad/t):

| | |
|---|---|
| loop | 70 ticks |
| tail peak | **60 px/t** at tick 24 of the run — where `Item153` plays |
| tail over 25 px/t | 46 ticks |
| reach | the whole body, 42 tiles, straight out from the head at full arc |
| settled | 93 ticks; the head goes back to chasing at 50 while the body finishes |

**Aiming** is which way the loop turns. The tail comes off the coil on the side the loop
did NOT turn toward, so it turns away from you: you left of its line, it circles right,
the lash takes the left. Dead ahead it circles down so the lash comes over the top rather
than under the ground.

**Hits** on a swinging segment are swept — the line it moved along this tick, 52 px wide
— because at sixty pixels a tick a point test misses a standing player half the time.
Damage ×1.25 at the neck rising to ×2 at the tail.

**The throw.** A hit writes straight onto `Player.velocity` (so Cobalt/Ankh do nothing):
the unit tangent of the segment's own path this tick, times its speed × 0.34 × 4, floor
8 px/t, no ceiling, no lift (a downward tangent goes down, into the ground, and comes
off it). Applied in `ModPlayer.PostHurt`, because vanilla writes its own knockback onto
the velocity after `OnHitPlayer` and before `PostHurt`; applied earlier it was silently
overwritten. The hit uses its own immunity slot (`ImmunityCooldownID.DD2OgreKnockback`)
so it lands through the forty ticks a brush from the head or the coil leaves behind.

**The ragdoll** (`RagdollPlayer`). No control, no items, wings and boots inert. Spin if
thrown upward past 8 px/t, roll otherwise. Vanilla's fall cap is lifted while airborne.
Contacts are read as speed the engine took away (above 16 px/t it moves in sixteen
pixel steps and returns the sum of the steps it managed, so a body that met the floor
on step three of five still comes out with two steps of fall on it; a zero test misses
that). Floor: back up at 0.45, sideways × 0.75, re-spun; under 2.5 px/t it lands and
lies for 90 ticks. Ceiling 0.45. Wall 0.5, spin reversed.

**The tell.** See 2.7. The head sounds the note and shows the star as it stops to loop
(half a second of coil before anything can throw), and every segment wears the outline
for the whole crack. The head itself is never outlined: it does not throw.

**Three ways it was wrong first, recorded so they stay wrong.** A rigid rod about a fixed
segment (a bar). The same rod with a scripted wave down it (a wave through a bar). And a
rope with momentum and a length constraint, one pass a tick — the right SHAPE, and a tail
at 280 px/t whatever the head did, because a chain that cannot stretch or bend puts
everything into its tip; slowing the head did not slow the tip. The angular chain keeps
the shape and puts a ceiling on the tip.

## 2.7 CC bypass — the rule

Anything in the mod whose hit goes through crowd-control immunity says so, always the
same way, so the player learns one signal (`Content/Combat/CCBypass.cs`):

- **The colour** is `CCBypass.Warning`, (255, 222, 66). Nothing else in the mod is that
  yellow.
- **The outline.** While an NPC or projectile is in the state it wears eight copies of
  its own sprite in that colour with no alpha, three pixels out in a ring, under the
  sprite — the Greatbow's charge-glow method. Drawn by the globals off
  `PAD().CCBypass`, which the owner sets every tick it applies (cleared before each AI).
  An entity that draws something other than its registered texture implements
  `ICCBypassOutline`.
- **The star and the note.** `CCBypassStar` is a particle (PADParticle): the soft
  white-on-transparent star `Content/Combat/CCBypassStar.png` (34 px, premultiplied,
  fades to nothing at the edges with a faint bloom, the Calamity sparkle way), drawn
  additively in the yellow at 0.85, two thirds of a player. It rides on the entity's
  centre and blinks, three ticks on, three off, for 30 ticks.
  `Content/Sounds/CCBypassWarn.ogg` plays once, one instance.
  Both fire on the tick the flag comes on, unless `PAD().CCQuiet` is set, in which case
  the whole calls `CCBypass.Warn(npc)` from the part that should announce it.
- **Nothing bypasses quietly.** A new attack that ignores knockback resistance, shields,
  or immunity frames sets the flag or it does not ship.

## 2.8 Drops — **DESIGNED; the whip is built**

Everything comes off the animal: a mouth, a hand, a tooth. Nothing here is a generic
slot-filler, and nothing should be.

**Shape.** Megashark's precedent is one drop, table wiped (`ModifyNPCLoot`). This boss is
bigger than that, so the decision is explicit: a **material plus recipes**, not a random
pick from a weapon pool. No RNG, every build served, and the player makes tools out of
what was people, at a workbench, with quantities. That reading is the point and nobody
says it out loud (§XXVII).

### Ready to build

| Class | Item | Notes |
|---|---|---|
| Melee | **Gaping Jaw** | Left click bites. Captures an NPC under a threshold, which goes **non-lethal** and is dragged; after a hold it is crushed. Second attack is a fast short-range bite, ~30. |
| Summon | **Joint Arms of the Defiled** | **Built.** A whip, every player's own off the kill (in the bag in expert). 30 damage, a 35-tick swing, Firecracker's reach, no tag. Five cracks in a row that land charge the next, which gives Many Hands (50% summon damage, 11 s). Spatters blood where it strikes, on the animal's scale (`JointArmsOfTheDefiledProj`). Built on `Content/Projectiles/Base/BaseWhip.cs`. |
| Summon | **a hand minion** | **[GAP]** A whip is a tag weapon, not a class. A severed hand that crawls or hauls itself along and grabs things. Without this, summoners get half a reward. |
| Ranged | **a bow of teeth** | With teeth-tipped arrows as its ammo. |
| Lore | **the number** | First kill only. The tally-plate says the number and the animal shows what it looks like (§XXI); this is where the player is given it. Uses the existing `LoreLock` system. |
| | Trophy, mask | A hand nailed to a board; the split head. |

### Tuning notes, so they are not rediscovered

- **Gaping Jaw.** A five-second hold is 300 ticks of one enemy not dying, which is dire
  DPS unless the crush pays: scale crush damage off the victim's **max HP** and splash it,
  so walking something fat into a crowd is a decision. A flat 300 HP threshold captures
  essentially everything pre-Wall and expires in hardmode — a **fraction of max HP** keeps
  working. Write the release rules early: weapon swap, death, distance, despawn.
- **The whip.** Vanilla whips run knockback **0 to 1 on purpose** — shoving the target away
  from your minions is a straight DPS loss. Durendal is 0, Snapthorn is 1. High knockback
  fights the class. Either drop it and lean on the tag bonus, or keep it and accept this is
  a *space-control* whip with a weak tag. 35 damage is also high for pre-Wall; Firecracker
  is 40 and that is post-mech. **22 to 26** with a strong tag. *Built at 30 damage with no tag, by
  request; the streak and Many Hands are what it gives instead.*
- **The arrows.** There is essentially **no lifesteal pre-hardmode in vanilla**, and that is
  deliberate; 1 to 3 HP an arrow with a fast bow out-heals the tier. Gate it: on crit only,
  or an internal cooldown, or only below half health. And ammo is a poor trophy — the drop
  should be the **bow**, with the arrows as its infinite ammo.

### Held back

- **Everhunger** (a long biting mouth along the aim line) and **Court of Hands** (arms out
  of the ground, slow and damage over time) are **incantations**, and the memory system
  they need does not exist. See `PAD_Item_Plan.md` §4. They should not ship attached to
  this boss; the first seal deserves its own introduction rather than arriving as loot.
  When they do land: both are ground or horizontal, so **the mage kit would have no answer
  to anything above the player** — one of the two has to go up.
- **Bewitching table is the summoner station** (+1 minion). Not a mage recipe station.

### Open

- **[GAP] Nothing does the throw.** The most distinctive thing in the fight is the hit no
  shield stops, and `CCBypass` and `RagdollPlayer` are already built. A player weapon that
  ragdolls *enemies* along the swing arc, wearing the same yellow, would be the strongest
  echo of the boss available and most of the machinery exists.
- **Naming.** *Court of Hands* and *Everhunger* are good. **"Defiled" and "the Wronged"
  both tell the player these were victims**, and §XXVII says nothing about this animal is
  explained anywhere in the world — drop names are where a player looks closest.
- The post-Moon Lord one gets the arsenal. This one gets what a street-length animal can
  spare, and should drop **less**.

---

# 2.5 · Boss Checklist

**Nothing is automatic.** Every entry is a `Mod.Call("LogBoss", ...)` from
`BossLogSupport.cs` at the mod root, sent in `PostSetupContent` only if Boss Checklist is
loaded (`ModLoader.TryGetMod`). No reference, no dependency. The call carries the
progression number, the downed flag as a `Func<bool>`, the head NPC type, and a dictionary
with `spawnInfo` (a `LocalizedText`, key `Mods.PisAndDag.BossLog.<Name>.SpawnInfo`) and
optionally `collectibles`.

| boss | progression | downed flag |
|---|---|---|
| Megashark | 11.5, after the mechs | `PADWorld.DownedMegashark` |
| Abomanation | 5.5, after Skeletron | `PADWorld.DownedAbomanation` |

Both flags are world data, set in `PADNPCs.OnKill`, sent with `WorldData`. A new boss is one
more `log.Call` and one more hjson line; forget the call and it is not in the list, forget
the line and the entry has no spawn text.

An earlier version of this section claimed Boss Checklist found bosses on its own. It does
not. That claim came from grepping a partial copy of the mod.

---

# 3 · Megashark

See `Content/NPCs/MegaShark/Megashark.cs`. Three phases plus an enrage, health drives the
phase, each phase opens with a scripted hand-over that owns the camera. Documented in the
file itself; not repeated here.

---

# 4 · White Death

One attack and a stub. Needs a plan before it needs code. Lore: the sniper, §8 of
`PAD_Lore_Planning.md`.

---

# 5 · Foigh, the Swordsman

The Clanmaster of `PAD_Lore_Planning.md` §9, under the name you asked for him by and his
art's folder has. A grey-haired man with a red eye and a sword twice his height, which he
drags. An optional boss, after the Moon Lord by the lore plan, fought in the Empty Hall
(`Content/Subworlds/EmptyHall.cs`, walled, roofed and lit for it).

**His road, his three phases and his end are built**: the first, the passage into the
second, the second, the passage into the third, the third and what is its own in it, what
keeps him from being cut down in seconds, what reaches a player from his second phase, the
ball against what fights for a player, what fights for a player turned aside and capped, the
ball by phase, and his end (PADWorkFlow §16.43 to §16.55). Whatever brings him to the hall
is not: §5.14.

Everything here was run in a rig on the game's own engine (its NPC update with tile
collision, its projectile update, `Player.Hurt`, an item's swing). Of play, there is one
fight of yours on §16.52's build (§16.53: his slashes could not be parried, minions shred
him), and an hour and twenty minutes in the hall on §16.54's, what was done there not known
(§16.55); the rest has **never been seen in play**. Numbers marked **yours** are as given;
**measured** came out of that rig; the rest are mine and want confirming.

## 5.1 What is built

| | |
|---|---|
| `Foigh.cs` | the NPC: numbers, moves, what is sent with him, the words a player's machine sends the server, the one tick of his AI, `/foigh`'s call |
| `Foigh.Scene.cs` | the road: his lines, its ticks, what the player it is for is shown |
| `Foigh.Passage.cs` | the passage into his second phase: its lines, its ticks, what the player it is for is shown, the blood, the sparks, the light up the blade |
| `Foigh.Third.cs` | the passage into his third phase: its lines, its ticks, what the player it is for is shown; the blood, the wound, the black and red on the sword; and his third phase's own move, the three cuts |
| `Foigh.Fight.cs` | the fight: every move of his first two phases, what he picks in all three, how long he waits, how his second phase quickens and lengthens the sword's moves, the sword against the player, where a player in the air will be |
| `Foigh.React.cs` | what he does about what is sent at him; `FoighMark`, a shot's verdict kept on the shot |
| `Foigh.Hold.cs` | what keeps him from being cut down in seconds: the cap, his posture, the holding parry, the run behind it, the break-out (§5.11) |
| `Foigh.Called.cs` | what is called up to fight for a player: turned aside at his usual rate, a cap of its own, the Stardust Dragon's one wait (§5.16) |
| `Foigh.Reach.cs` | what reaches them from his second phase: the hook, the two cuts of wind, what he reads of a player (§5.12) |
| `Foigh.Wild.cs` | his third phase, wilder: half as fast again, a cut let go, the wheel, the sword driven deeper, lingers and feints, what he learns of a player (§5.13); and in all three phases the ball (§5.15), by phase (§5.17) |
| `Foigh.End.cs` | his end: the step to the hall's end, the roar and the flames, the fold, the lines and the answers, Finish and Spare (§5.17) |
| `Foigh.Draw.cs` | drawing him from his pieces, the blade's trail, the ghosts of a flash step, sparks, dust, sounds |
| `FoighRig.cs` | where everything is on his art, and the sums that put seven pieces together for any pose. No game in it |
| `FoighTrack.cs` | a movement as poses at moments, eased between |
| `FoighMoves.cs` | every movement he has, and the ticks things happen at in each |
| `FoighPlayers.cs` | the player's side: the walk up, put down a long way off him for a passage, wings not drawn once he has cut them (`FoighScenePlayer`); held, thrown or shoved, a grapple let go and wings shut by a hit of his, pulled in on his hook (`FoighGripPlayer`); what the game has just settled of their jump and fall, for his sum of where they will be (`FoighAirPlayer`); held, put down, walked and turned at his end, their arms with his sword in their hands (`FoighEndPlayer`) |
| `FoighDagger.cs`, `.png` | his throwing knife (the picture is a stand-in: PlaceholderTextures.md) |
| `FoighSwordThrown.cs` | his sword, thrown, and standing in the ground |
| `FoighReturned.cs` | a shot of a player's on its way back to them |
| `FoighSlash.cs` | a cut of his third phase's, let go of: the flying slash (no picture of its own); and its pale kind, his second phase's cut of wind |
| `FoighLine.cs` | a straight cut where nothing is, shown and then open, each with its own ticks: out of the ground where he drives his sword deeper, and about the ball, white in his first two phases (no picture of its own) |
| `FoighHook.cs` | his hook in the air (the Meat Hook's two pictures: PlaceholderTextures.md) |
| `Content/Buffs/UltraBleeding.cs`, three `.png` | the wound his sword leaves in his third phase, as three buffs, and the blood it spews (the icons are stand-ins) |
| `Content/Particles/BurnSmoke.cs`, `BloodSlash.cs` | the black smoke with a red rim; a cut left hanging in the air, red with its black twin |
| `Content/Particles/AshDrift.cs` | a fleck of ash that lies in a heap and is then blown away: him, at the end of Finish |
| `SwordsmanBloodSide.png` | the sword from the passage into his third phase on (a stand-in) |
| `Content/Items/Melee/FoighsRustSideSword.cs`, `.png` | Foigh's Rust Side Sword, what Finish gives: a weapon of its own since §16.57 (its files and how it fights: PAD_Item_Plan; its picture a stand-in: PlaceholderTextures.md) |
| `FoighCommand.cs` | `/foigh`, `/foigh second`, `/foigh third`, `/foigh end`, `/foigh gone`, mod root |

He is **seven pictures, not a sheet**, each turned about its own joint: your head, torso,
two arms, two legs and sword, used as they are. A movement is poses at moments; the fight
says which movement and which tick; the drawing, the blade that hits and the trail behind
it all ask the same sum. If a piece is exported again at another size, its numbers at the
head of `FoighRig.cs` are what has to change (size, the joint on the picture, the limb's
length).

From his second phase the sword is **another picture, its sharp side**:
`SwordsmanSharpSide.png`, which is a stand-in made from `SwordsmanBluntSide.png` by
recolouring its blade (same canvas, same outline, the grip in the same place, so it is
drawn by the same numbers: a real one has only to keep those). `FoighWhetstone.png` is the
stone, ten by six, another stand-in. Both are in PlaceholderTextures.md.

He does **nothing by touch** (damage 0). The sword, the knives and the hand are worked out
on each machine against its own player. Because the game does not scale an NPC that does
nothing by touch (`NPC.ScaleStats`, 1.4.4), his life is set in `SetDefaults` for the world
as it is.

## 5.2 Numbers

| | Classic | Expert | Master | in Legends |
|---|---|---|---|---|
| life (**yours**) | 150,000 | 240,000 | 360,000 | x1.5: 225,000 / 360,000 / 540,000 |
| the sword, over 66% of his life (**yours**) | 100 | 130 | 150 | x1.5: 150 / 195 / 225 |
| the sword, from 66% down (**yours**) | 150 | 200 | 230 | x1.5: 225 / 300 / 345 |
| the blow down in the third move (**yours**) | twice the sword | | | |
| a knife; a shot sent back (**yours**) | 100 | 100 | 100 | 100 |
| a flying slash, in his third phase (mine: half of the row above) | 75 | 100 | 115 | x1.5: 112.5 / 150 / 172.5 |

All of it through armour (all of a player's defence ignored), varied as the game varies any
hit. Defence 40 (mine), and 80 in his third phase (**yours**: doubled). Not grown by how
many are in the world. Stages by his life: over
66%, to over 30%, from 30% down (**yours**); the sword's damage follows them, and at 66%
the passage of §5.8 begins, and at 30% that of §5.10.

How far a blow throws a player standing still (**measured**): a cut about 7 tiles, the
thrust 13, the blow down 12, a knife 4, a swing of theirs met 10, the thrown sword 13, the
fling off the sword 28, a flying slash 6.

## 5.3 The road

Await, Turn, Talk, Draw, Vow, Begin: six states before the fight, and nothing can land on
him in any of them.

1. He stands with his back to whoever has come, the sword in the ground beside him.
2. On the player's machine the picture fades (30 ticks); under the black they are put down
   28 tiles behind him, and it comes back up (36); they walk to **20 tiles** (**yours**) at
   half a walk's pace, nothing in their hands and nothing able to touch them.
3. The camera goes over to him (54 ticks). He looks back over his shoulder, and turns
   round (112).
4. Three lines (**yours**, word for word), each with the one answer "...".
5. He takes the sword out of the ground behind him, swings it up over his head and lets it
   down behind him, into the stance he fights from (108).
6. The fourth line (**yours**), and "Ready".
7. His name comes up under him ("Foigh", his title "The Swordsman" with it) and goes to
   the bar; black; the camera back on the player; and he begins to walk, 3.5 s after Ready
   (**measured**).

The server waits for the player's three words (walked up, talked, ready) 25 s, 90 s and
90 s, and then goes on by itself. If the player it is for dies or leaves on the road, he is
gone. No music on the road; `MusicID.OtherworldlyBoss2` in the fight (`FightMusic`).

`FirstPhaseLine` in `Foigh.Scene.cs` is the line your list asks for as the fight begins
("Auto skip dialouge box"), in a box that goes by itself (`Dialogue.Aside`). It is empty:
no words were given.

## 5.4 Phase one

**Walking** at them at 1.15 px a tick (a player walks at 3), the dull side of the sword
dragged behind him, to rest about 54 px off. Between moves, 26 to 70 ticks of it. What he
does next is by where they are (**measured**, 600 picks each):

| where they are | what he picks, by weight |
|---|---|
| within 150 px, no more than 84 px over him or 70 under | one 3, three 3, two 2, four 2 |
| more than 150 px over his head | knives 4, four 2.5, two 2 |
| anywhere else | knives 5.5, behind 2.5, and two 2 within 30 tiles |

The move he did last is a quarter as likely.

| move | what | ticks |
|---|---|---|
| **one** | wound up over his head; the cut down (lands at 40), the cut up (58), turns to face them, the thrust with a lunge of about 74 px (lands about 89), and a leap back with the sword coming round after him, which cuts too | 150 |
| **two** | down into his knees (18), a leap onto them, point first, aimed a third of the way ahead of where they are going, chosen up to 30 tiles off. **How high it goes is by how far off they are** (**yours**, §16.44: from the height that was to three times it): 75 px beside him, 150 px from 15 tiles, 225 px (14 tiles) from 30, and so 33 to 57 ticks in the air; onto a player who is over him, at least 24 px over their feet. On them: 12 ticks, then 3 in 5 into the first move from its second cut, 2 in 5 into twice round. On nothing: the sword is in the ground **two seconds** (**yours**), and 34 ticks more to be back in his stance | |
| **three** | the bottom hand: wound up and held, the blow down for **twice the sword** (**yours**; lands at 51); the whole of him round once with the sword out at arm's length (20; it cuts behind him and in front); a knife off the top hand | 138 |
| **four** | a jump of ten tiles; the sword thrown at where they will be at 24 px a tick, wearing the yellow star: the sword's damage, and whoever it goes through is down for **a second** after they land (**yours**); it stands in the ground. He comes down, runs to it (8.6 px a tick), takes it, and goes round twice with it | about 160 |
| **knives** | one (30 ticks, out of his hand at 13) or, 22 times in 100, three, **20 ticks apart** (**yours**; 70 ticks). 17.5 px a tick, level, gone at the first tile. After three, 40 times in 100, the leap | |
| **behind** | a flash step to 58 px behind them (7 ticks, nothing can land on him); **half a second** with the blade low (**yours**); the cut up (lands at 31); a flash step back to where he stood | 52 and two steps |

## 5.5 What he does about what is sent at him

Only while he walks (**yours**: "while not attacking"). Decided on the machine of whoever
shot or swung, rolled once for each shot a little before it gets to him.

| | chance | what |
|---|---|---|
| a swing of theirs (an item swung; a spear, a whip, anything held out) | 3 in 5 (**yours**) | met: it does not land, and they are thrown back about ten tiles whatever shield they have (the yellow star) |
| a hard shot (ranged or thrown, solid by the parry's count: an arrow, a dart, a knife, a bullet that stops at the first thing) | 2 in 5 (**yours**) | struck out of the air; half of those (**yours**) go back at whoever sent them for 100 through armour, broken on the first tile |
| anything else thrown (a spell, a sword's light, a boomerang, fire, a rocket, a bullet that goes through things) | 3 in 5 (**yours**) | he is not there: a flash step to in front of them and into one or three, or up over it and down on them with the leap |
| a minion, a sentry, and what those shoot | 2 in 3, 4 in 5 in his third phase (**yours**), in every phase and every move (§5.16) | turned aside: a minion unhurt, a shot of it struck out of the air; and what lands of it no more than half of the cap in any second |

**Measured** over 400 of each: bullets 178 struck and 85 sent back, arrows 154 and 71,
throwing knives 167 and 100, a spell 239 stepped out of the way of, rockets 244, swings
235 met.

In his second phase each is two times in three (§5.9), in his third four in five
(**yours**; **measured** over 300 of each: bullets 222 struck, a spell 239 stepped out of
the way of, swings 244 met). From his second phase, what keeps coming at him is not
answered a shot at a time but whole, and in whatever move he is in: §5.11.

## 5.6 The one who drank

A healing potion drunk within 100 tiles (known by the wait for the next one beginning). If
he is walking, throwing a knife or at the tail of a move he comes at once; in the middle of
something longer he keeps it in mind two seconds and no more.

A flash step to beside them; the bottom hand to their throat (the yellow star; closes at
tick 10); held there at arm's length (40; their feet a pixel or two off the ground: he is
not much taller than they are); thrown up as the sword comes up (16); the sword up through
them about six tiles over the ground, held (30); flung off it as a ragdoll, about 28 tiles,
down a second after they land. One blow in it, as the sword goes through: the sword's
damage, that nothing dodges and no earlier hit's grace stops.

The hand can be got out of the way of: a run in good boots (7 px a tick) or a jump as he
comes, and it closes on nothing and he is 40 ticks getting over it (**measured**). Walking
away is not enough.

## 5.7 Parried

His cuts can be turned aside by the universal parry (`Parry.AddNPC`): the first move, the
leap, the third move's blow and turn, twice round, the cut from behind, and in his second
phase the blow down and the cut up of the hand's move, every cut of the mill and its last
blow, and the late cut's line as it opens (whichever way they face: it lies through them).
Not the thrown sword, not the hand (either of them), and since §16.47 not the cut up out
of the ground nor the blow in the air (nor that blow when it ends on the ground): those
wear the star (§2.7). He reels 44 ticks; on every third he is down on a knee two seconds,
open to the riposte (three times the blow). A knife and a shot sent back are broken by a
parry. Since §16.50 each parry of his sword also wears at his posture by 25 in a hundred
(§5.11): four of them, close enough together, break him.

## 5.8 The passage into the second phase

At 66% of his life (**yours**), once, from whatever he is doing: a move is cut short where
it is, a player in his hand is let go of, and from the air he comes down under the black;
only a flash step is finished. Five states, `Spent`, `Knee`, `Whet`, `Oath`, `Resume`, and nothing can land on
him in any of them. Only the player he fights is moved, held and shown it. All of it
**yours** in what happens and in what order; the ticks are mine (**measured** where they
come out of the rig).

1. He stops, rocks back and bows; the bars come in and the picture goes to black (26
   ticks).
2. Under the black: what he had thrown is gone, the sword is in his hands, and he is on
   one knee before it. It stands **upright** in the ground, driven in until its handle is
   at his chest (52 of its blade's 72 px under it), **both his hands on the handle** and
   his head bowed over it (**yours**, §16.45: "perpendicular and hes holding on to it",
   and of the two ways drawn, "driven in deep"). The player is put down **70 tiles** off
   (**yours**), on the side of him they were on (his other side, or nearer down to 30,
   where there is no ground), on their feet and facing him.
3. The picture comes back on the player; the camera goes over to him (1.6 s) and leans in.
4. His head down, he spits blood **four times, a second, 0.4 s and 0.3 s apart**
   (**yours**), each bending him further over the sword, which does not move. He looks up.
5. Two lines (**yours**, word for word), each with the one answer "...".
6. His head goes down to the sword and he sets himself over it (10 ticks); he heaves,
   coming up off his knee, and it comes 9 px up its own line (16); he tears it out forward
   through the ground, the floor heaving up along its way (8), and has it up at the sky,
   held. (Mine, §16.45: driven that deep it cannot be lifted clear.) Down before him, a
   stone from his belt, and the sword drawn back through it from guard to point: sparks,
   and the blade behind the stone is the sharp side. A light up the blade, twice. The stone
   away, the sword over and down behind him (160 ticks in all; 150 before §16.45).
7. The third line (**yours**), which has no answer and goes by itself 80 ticks after it
   has all arrived. While it is up his bottom hand goes back behind his hip and comes
   forward with **a hook on a chain** (**yours**, §16.51: "he js pulls it out and doesnt
   mention it"): in his hand from the 54th tick of it, heard once, named nowhere (§5.12).
8. Black; the camera back on the player; and he walks, 70 tiles off, with 70 ticks before
   his first move.

About nineteen seconds with both answers given at once. The server waits 90 s for the two
answers and 15 s for the third line, and then goes on by itself. From then on the sword
is its sharp side in his hand, thrown, and in the ground; the damage is the 66%-down row
of §5.2; and he fights as §5.9 has it.

`/foigh second` leaves him two thirds of his life less one, to see it.

## 5.9 Phase two

By your list of §16.46, which is quoted there with every reading taken of it. Everything
of his first phase is still his; what is different:

**What is sent at him** (§5.5): each of the three answers is **two times in three**
(**yours**). Of the shots struck, half still go back. **Measured**: of 400 bullets 253
struck and 125 sent back, arrows 265 and 130, throwing knives 260 and 116; of 3000 spells
2027 stepped out of the way of; of 3000 swings 2037 met.

**What he picks** (**measured**, 600 each; the weights are mine, what they are for is
**yours**: mostly walking from far off, the leap more often, the knife more rarely; and
since §16.47 less of his first phase in it):

| where they are | what he picks, by weight | before §16.47 |
|---|---|---|
| within 200 px (it was 150), no more than 84 px over him or 70 under | the hand 3, the mill 3, the cut up 2, the late cut 1.5, in the air 3 if they are off the ground; one 1.2, three 1.2, two 0.8, four 0.8 | the hand 3.5, one 2, three 2, two 1.5, four 1.5 |
| more than 150 px over his head | in the air 4.5 within 720 px along and up, two 2.5, four 2, knives 1 | two 3, four 2.5, knives 1.5 |
| anywhere else | **walking on 9.5**, two 4.5 within 40 tiles (it was 30), the late cut 1.5 within 39 tiles, the mill 1.2 within 380 px, in the air 2.5 if they are off the ground, behind 0.8, knives 0.7 | walking on 6, two 3, behind 1.2, knives 0.8 |

Walking on is not a move: he goes on at them for 40 to 70 ticks and picks again. The late
cut and the mill go along the ground: they are only picked at one who is about on it with
him (the first row's band). Off the ground is nothing solid within 64 px under their feet.
His first phase's four are 4 parts in 13.5 of what he does up close, where they were 7 in
10.5.

**How long he waits**: after a move picked with them in reach, **60 to 120 ticks**
(**yours**: "wait 1-2 sec in between attacks"), and so after the first or third move he
steps into out of the way of a spell. A swing of theirs that he meets in that time does
not cut it short (mine). After anything else, what that move gave him before; after a
parry, 18 to 30.

**The sword's four moves** are **a quarter faster and carry him a third further** (mine in
the amounts; **yours**: "greater speeds and ranges"). The move's clock goes on by a tick
and a quarter, and everything in the move is read off it. **Measured**: the first move 121
ticks where it was 151, its steps 158.6 px where they were 117.9 (a step that asks for
more in a tick than his fastest 15.5 px has the rest carried into the ticks after, here
only); the third 112 ticks where it was 139, its step 39 px where it was 30; the leap's
crouch 16 where it was 19 and the sword out of the ground in 28 where it was 34; the
fourth's parts 14, 8, 7, 29 and 21 where they were 17, 10, 8, 36 and 26. Not quickened:
anything in the air, the two seconds in the ground, the knives, the step behind, the one
who drank. The leap back at the end of the first move is 192 px where it was 170, and
lower; he runs after the thrown sword at up to 10.6 px a tick.

**The hand** (**yours** in what happens and in what order; the ticks and everything not
named in the list are mine):

| | what | ticks |
|---|---|---|
| the sword up | from where it drags to over his head | 16 |
| the run | at them with it up, 9.5 px a tick, until they are within 84 px (or a wall, or a second). In reach already, he does not run | to 60 |
| down, and up | a step and the blow down into the ground, the floor heaved; a step and the cut up. The sword's damage each (lands at 12 on a player standing in it); can be parried | 42 |
| the knee | down on it, the sword behind him, the hand drawn back; the yellow star from here. He looks for them once and does not turn again | 18 |
| the leap | forward and up with the hand out: **as far as 10 tiles and as high as 5, in half a second** (**yours**), aimed at where they will be, smaller when they are nearer, over them if they are over him | to 30 |
| caught | the rest of the way down with them; they are pulled in to his hand over 5 ticks | until he lands |
| the sword through | pulled in against him, the sword over his shoulder to point at them (18); its point in (22: one blow, the sword's, through armour and any earlier hit's grace); pushed on through them for a second | 78 |
| raised | until it points 48 degrees up from level ahead of him, them on it by its guard, 22 px off their feet; held | 46 |
| flung | as the one who drank is: let go at 10, about 25 tiles, down a second after they land (mine: nothing was said of what comes after) | 40 |
| or nothing | the hand closed on nothing: 40 ticks on a knee | 40 |

The camera is in at 1.4 on whoever he has, from the catch to the end of the raising.

**Measured**, the leap: 160 px forward, 80 up and 30 ticks at its furthest. It has a
player standing 30 to 200 px off and not one at 230; one 60 px over the ground and 100 px
off; one who walks at him from 260 px. It does not have one who runs from the moment he
goes down (7 px a tick: 20 tiles off when he lands), nor, narrowly, one who only walks
away from 150 px. With them behind him by the time he leaps, he leaps the least there is
(40 px) at nothing.

**Four more moves of its own** (§16.47. **Yours**: three more moves in the manner of the
fights you named, one that goes after a player in the air ("flash step and no gravity to
strike you falling down"), his second phase only, and less of his first in it. Everything
else in them is mine: PADWorkFlow §16.47 has every reading and every number):

| | what | ticks |
|---|---|---|
| **the late cut** | sunk low with the sword at his hip, a pale line from him through them (`Poise`); gone along it, a flash step, to 96 px past them and no more than 45 tiles; past them with his back turned (`Wake`), and 20 ticks after he has stopped **the whole line opens**: the sword's damage to whoever is on it, and thrown the way he went | 26, 7, 66 |
| **the mill** | the sword levelled at them (`Level`); three turns coming on at 7.2 px a tick with it out, a cut each time it comes round behind him and ahead (`Mill`); up out of the last turn and a blow down as he lands, the floor heaved (`MillEnd`). The sword's damage each | 22, 48, 50 |
| **the cut up out of the ground** | a run at them with the sword dragged in sparks, 9 px a tick, 10 ticks at the least (`Scrape`); the blade torn forward under him through the ground and up (`Upcut`). Whoever it lands on is **thrown about 12 tiles into the air**, and he is up after them (the next row) | 10 to 60, then 46 (20 if it lands) |
| **in the air** | a flash step to beside where a player who is off the ground, or over his head, **will be** as his sword comes down; a moment there with no weight, the sword going up (`Hang`); a blow all the way round under him that **sends them down** (`Smite`); he falls after it and lands (`Drop`, `Alight`). With under 76 px of air under him, the mill's last blow in its place, down onto the floor with him | 7, 14 (6 after his own cut up), 17, the fall, 24 |

The cut up out of the ground and the blow in the air **wear the yellow star** (§2.7): the
first throws through any shield, and the blow that follows it lands whatever grace the cut
left them. Neither can be parried; the late cut and the mill can (§5.7). The cut up and
what follows it are two blows of the sword, 300 / 400 / 460 by the world: mine, and yours
to say (RELEASE_TODO).

Where a player will be is the game's own sum of how a player goes up and down, a tick at
a time (since §16.48, read in its assembly and held against its own `Player.Update`: a
jump, every pair of wings flying, hovering and gliding, rocket boots, a flying carpet, a
slow fall, weight turned over, one who jumps again as they land, platforms, ceilings, and
one he threw: out by 0.003 px at the worst), so one who jumped is met where they come
down and one who flies where their wings will have them. Sideways they are taken to go on
as they are going (RELEASE_TODO). A hook in something or a rope is taken to hold them. He
goes to their side with his feet 6 px under theirs; or
level with them or 10 px over (they are stood on something); or 16 px under (their head is
against a ceiling: he is the taller). With no room beside them at all he throws a knife.

**Measured.** The late cut: out of it by a jump as it opens, a run on past its end, or a
moment in which nothing hurts them, and not by walking; feet under 33 px off the ground
are on it; turned aside by a parry begun 5 to 12 ticks before it opens. The mill: 328 px in
the three turns and 66 more in the blow; a player kept ahead of him is cut at ticks 14, 30
and 46 and by the blow, one kept behind at 6, 22 and 38. The cut up: the blow at its tick
11, thrown up at 12.5 px a tick to 189 px; he is 46 px to their side with his feet 6 px
under theirs as the next blow lands, about half a second after the first; out of it by a
run from the moment he sets off or a jump over him at a run, and not by standing, walking
off or a jump in place. In the air: his sum of where a falling, jumping or thrown player
will be is within 4 px of where they are, 20 to 50 ticks on (one who falls slowly, and one
whose weight is turned over, among them). In three whole fights of four minutes the rig's
player, who gets out of the way of nothing on purpose, was cut by the late cut 16 times of
17 and thrown up by the cut up 17 times of 19, and he went up after one who was off the
ground by themselves 10 times.

## 5.10 His third phase

At three tenths of his life, once (§16.48. **Yours**: every beat of it and their order,
the two lines marked, Ultra Bleeding on the sword, the wings cut and flight gone from
then, the fade back, black and red as in the picture you sent; and by your answers how
the wound stacks, what loses flight and for how long, and the four lines' order. The
ticks, distances, looks and the two other lines are mine: PADWorkFlow §16.48).

| | what | ticks |
|---|---|---|
| `Spent` | as at two thirds: he stops, nothing lands on him, the player is held, the picture goes to black | 26 |
| `Shudder` | under the black the player is stood 16 tiles off him and the camera put on him; the picture comes back on him bent over his sword, which is in the ground at a slant, both hands on it, shaking; two coughs of blood; his head up, and **"Ahh... blood. Mine. I had forgotten the taste of it."**, which waits for its one answer ("...") | to 156, then the answer |
| `Gut` | not a word. The sword out of the ground, up at the sky in both hands, tipped back over his shoulder and let forward through his hands until he has it by the blade with its point at his belly, and through him to the guard | 162 |
| `Bled` | torn out, up over him and flicked down, the blood leaving along its swing; the sword bloodied from here. *"The muscle hardens to rust, the sword bleeds with the body"* (yours), by itself; then he looks up: **"It is fairer so."**, by itself | about 420 |
| `Flash`, `Sever` | a flash step to 42 px behind them; his hand on their wings, a pull, and the sword down between his hand and their back into the ground. **From the next tick nobody flies.** He leaves the sword standing, comes away a step with what he cut off held up, and says *"Futile devices"* (yours), by itself | 7, then about 110 |
| `Resume` | black at once; under it he is back where he stood with the sword in his hand, and it has taken; the picture comes back on the player, cuts of red and black hanging about him | 60 |

About 21 seconds as played. The server waits for the answer 90 seconds, and for the two
lines that go by themselves 15 and 10; a player who is not there holds nothing up.

**In his third phase:**

- **Ultra Bleeding.** Every blow of the sword that lands, held or thrown (not his knives),
  is one more wound, three at the most: 12 life a second each, no life coming back by
  itself, five seconds from the last blow in every difficulty, and when they run out all
  of it stops. Three buffs (I, II, III). One who dies of it "bled with the sword".
  Blood spews down out of every wound (§5.16).
- **Nobody flies** while he is there with the cut made: wings, the booster and flying
  mounts do nothing (the mod's own lock, `PisPlayer.AllowedWings`), and cut wings do not
  glide or quicken the air either. The wings stay in their slot and are not drawn.
  Everybody, not only the one he cut (RELEASE_TODO). Back the moment he is gone.
- **The sword burns black and red**: a red glow on the bloodied blade with soot on it,
  black smoke with a red rim off it, swings that leave a black sweep with a red line
  along it, a cross of red and black where a blow lands. No shader, no new picture but
  the bloodied sword's stand-in.
- **He fights with his second phase's moves** (but its wind), half as fast again, and with
  what is his third's own: the three cuts, below, and since §16.52 the rest (§5.13).

`/foigh third` begins it (from his first phase, without the second's passage).

**The three cuts** (`Rend`, §16.49. **Yours**: "he can also throw a flying slash projectile
too for one of the moves but make it a move set tho", and "remember the slash is multi
directional so if ur on the ceiling it also hits you". Read as one move of three cuts,
each letting one slash go at the player wherever they are; the other readings, several
moves, or several slashes fanned from each cut, are not built. Every number is mine).

| ticks | |
|---|---|
| 0 to 24 | the sword up over his head, held from 16; at 10 he looks for them; at 12 the blade flares |
| 24 to 31 | the first cut, down, a step of 20 px into it; let go of at 30 |
| 31 to 71 | the sword rests where it came down, he sinks over it; at 44 he looks for them |
| 71 to 77 | the second, up, a step of 14 px; let go of at 75 |
| 77 to 115 | up over his head in both hands, held from 107; at 89 he looks for them |
| 115 to 122 | the third, down with all of him, a step of 22 px; let go of at 122: the great one |
| 122 to 160 | the sword stays down until 140, and comes back to where he drags it |

- **What is let go of** (`FoighSlash`): a crescent of the black and red, 68 px from tip to
  tip, 12 px a tick for 80 ticks (59 tiles); the third 88 px, 9 px a tick for 100 (56
  tiles). Straight, through platforms, gone against the first solid thing, and gone when
  he is. Half the sword's blow through armour, once, **and a wound**. What hurts is its
  leading edge, not what streams off its back.
- **At them, wherever they are**: along the ground at one who stands on it (level, its
  lower tip 2 px clear of the ground), up at one who hangs from the ceiling, down at one
  below him, back over his own head as far as 45 degrees past straight up; half of where
  they are going allowed for. Not at one behind him: he looks for them before each cut
  and comes round (14 ticks), and in a cut he does not turn.
- **Jumped** (a plain jump clears all three: the first two if begun 14 to 36 ticks before
  they arrive, the third 18 to 30), **or turned aside by a parry** that faces it (it is
  gone; he does not reel). They come 45 and 47 ticks apart; a parry is 42.
- **The blade itself** cuts one near enough in each of the three, the sword's whole blow;
  parried there he reels, and nothing more is let go of.
- **How often**: never in his second phase. In his third, one go in ten with them in his
  reach; from further off, within 800 px of him, about one in six along the ground and one
  in four at one who is over his head. 30 to 60 ticks before his next move (§5.13; it was
  40 to 62).

## 5.11 What keeps him from being cut down

§16.50, the first of three deliveries of what was asked on the 7th. **Yours**: "he can do
something so shit dont just shred him ... zenith and last prism lk shredding him tf out.
(special parry? damage cap??/more flash step sudden cc bypass(0.2s recover time)?)", all
three picked; "he can start to run forward after parrying goes on for more than 2.5
secs"; "his second phase is basically a sekiro boss"; and the answers: posture; 0.8 in a
hundred of his life a second; what is not dealt kept up to five seconds' worth and dealt
while he is broken; the cap in all three phases and the rest from the second; four in
five in the third. Every other number is mine. All of it is in `Foigh.Hold.cs`.

| | Classic | Expert | Master |
|---|---|---|---|
| the most that can be taken from him in a second (**yours**: 0.8%) | 1,200 | 1,920 | 2,880 |
| the most at once (mine: two seconds' worth) | 2,400 | 3,840 | 5,760 |
| what gets through his guard at the cap, about twice a second (mine: half a second's worth) | 600 | 960 | 1,440 |
| kept for when he is broken (**yours**: five seconds' worth) | 6,000 | 9,600 | 14,400 |
| at the rate alone, his three phases last no less than | 42.5 s, 45 s, 37.5 s | | |

**The cap** (every phase). With nothing more that can be taken, he turns everything aside
(a shot struck out of the air, a blade or a beam through with him as if it had hit, a
swing met and its player thrown back) until half a second's worth can be taken again.
What burns or bleeds him comes out of the same. Worked out on the machine of whoever's
hit it is; anything taken beyond it is given back where the world is decided. His life
set by hand is not counted. Full again after each passage.

**His posture** (every phase; **no bar**, **yours**: "keep posture ui hidden").

| what wears at it, of 100 | |
|---|---|
| what is shot at him and turned aside (not what fights for a player: §5.16) | a sixtieth of its damage each (0.5 to 4; a shot struck by itself 4), **9 a second in all** |
| a swing met | 6 |
| his own sword parried | **25** |

Back by 12 a second after a second and a half let alone. Worn through: **broken** for
180 ticks (**yours**: about three seconds), thrown back a step, down on a knee, answering
nothing; the first blow on him is the riposte (three times the blow), none gets him up,
and what was kept can be taken. A second more with nothing wearing at it. Not with his
sword thrown, not between two places.

**The holding parry** (from his second phase). Three hits of a player's that would land,
no more than 15 ticks apart (four a second or more), are a stream. One roll a second for
all of it: **two in three** held (**four in five** in his third phase), in whatever move
he is in; the other seconds it lands. Of a second's hard shots held off, one goes back
at whoever sent them. Not what is called up to fight for them. **Measured**, a bullet
every four ticks for four minutes: 144 of 238 seconds held in his second phase, 195 of
238 in his third.

**The run** (from his second phase; **yours**: "after parrying goes on for more than 2.5
secs"). Held at it 150 ticks with no gap of 30: `Charge`, at them with the sword up at
10.6 px a tick, nothing of theirs landing while he runs. Within 92 px: the blow down and
the cut up, or his first move's three cuts, half each; up after one who hangs over his
head. A wall or 180 ticks ends it; the next needs 150 again.

**The break-out** (from his second phase; **yours**: the flash step, the star, a fifth
of a second). Strikes on his feet taking 5 thousandths of his life or more in a second,
no oftener than every 360 ticks: a flash step to behind them (in front with no room
behind), the cut up beginning 12 ticks after he lands, not turned by a parry, and he
stays there. Up beside one who is off the ground, with the blow in the air.

**Measured**, thirty seconds of each on him in a Classic world, against a player who
stands and is hit, before and now (38,420 is the most the cap lets thirty seconds take):

| | first phase | second | third |
|---|---|---|---|
| the Last Prism, before | 80,354 | 38,049 | 34,464 |
| the Last Prism, now | 38,360 | 13,771 | 12,997 |
| the Zenith, before | 68,848 | 68,534 | 58,960 |
| the Zenith, now | 37,984 | 24,440 | 18,871 |

## 5.12 What reaches them from his second phase

§16.51, the second of three deliveries of what was asked on the 7th. **Yours**: "he has a
meat hook on his bottom hand.. in his animation he js pulls it out and doesnt mention
it"; "He can jump super high"; "we need more projectile on phase 2-3 ... player can js
dodge by using hooks and rocket boots and wings"; and the answers: the hook "both, by
where you are"; pale wind cuts in his second phase; knife volleys; a hit lets go a
grapple, none thrown for a second; a hit in the air shuts wings and rocket boots for two
seconds; reads and punishes. Every other number is mine. The hook, the wind and what he
reads are in `Foigh.Reach.cs`; what a hit does to a player is in `FoighGripPlayer`.

**The hook** (from his second phase; in his third too). Thrown from 140 to 732 px with
nothing between: the hand back by tick 12 and through at 18, then 22 px a tick for 36
ticks, straight, at where they will be, stopped by the first tile. 50 through armour,
and none of a hurt's grace after it. A parry knocks it away.

| what it bites | |
|---|---|
| one stood on the ground (something solid within 64 px under them, no hook of theirs in anything) | **he comes to them** along his chain, 15 px a tick at the most, no weight; within 78 px the sword comes down, the blade down 10 ticks on; beside them in the air instead if they have left the ground; a wall or 50 ticks lets go |
| one off the ground, or hanging from a hook, or on the ceiling | **they come to him**: their hooks let go, a mount left, pulled at 15 px a tick at the most with none of the keys theirs (their hands are), to just ahead of him; the blow is begun with them 132 px from there and the blade is down as they arrive; 70 ticks at the most |
| nothing (a wall, the end of its chain, a parry) | he takes it in again, 36 ticks, open |

The blow is his sword's, whole, and can be parried. After it he waits as after any move
from up close. No break-out with his hook out or somebody on it.

**The leap, as high as they are** (from his second phase). It tops out 24 px over their
feet, up to **46 tiles** over where he left the ground (it was 225 px), and no higher
than leaves 28 px between his head and what is over him. Up at 15.5 px a tick for as
long as that takes; then his weight. **Measured**: at one who hangs 500 px up and 200
off, his feet get 536 px up and the point is on them as he goes by; at one 700 px up,
738 px.

**Two cuts of wind** (his second phase only). 92 ticks where he stands; a cut of wind let
go at them at ticks 19 and 64 (45 apart: a parry is 42): 14 px a tick for 66 ticks, 68 px
from tip to tip, **half his sword's blow**, no wound, broken by a parry or the first
tile. From up to 900 px.

**Knives.** Of a hundred throws 22 are three, **38 a fan of five at once** (from his
second phase), the rest one. A fan is twelve degrees from knife to knife: at one stood on
the ground the lowest is aimed at them and four go over it; at one off the ground the
middle one is aimed.

**How often** (weights, beside §5.9's and §5.10's): the hook 1.4 from far off along the
ground, 3 at one off the ground, 3.5 at one over his head; knives 1.6 from far off (it
was 0.7) and 2.5 at one over his head (it was 1); the wind 0.8 up close, 2 from far off,
2.5 at one over his head.

**A hit of his** (any of them, from his second phase; on the machine of whoever is hit).

| | |
|---|---|
| a hook of theirs in anything | let go, every one; none thrown for **60 ticks** (**yours**: a second), by the key or as a thing held |
| off the ground | for **120 ticks** (**yours**: two seconds) nothing holds them up: wings and gliding, rocket boots, the Soaring Insignia, a flying carpet, a mount that flies or hovers. What wings and boots had left is theirs again after, if they are still in the air |
| left alone | a jump in the air, what only slows a fall, everything in the hands |

**What he reads** (from his second phase). Seen by the player's own machine and told to
the server, one thing in 150 ticks; he takes one in 75. Walking or with his sword up, he
answers **two times in three** (**four in five** in his third phase).

| what | when it is told | what he does |
|---|---|---|
| a swing that missed | anything swung or thrust (a sword, a spear, the mod's own swords, a whip; not a parry) whose use ends with nothing of theirs landed on him, met or turned aside, from within 240 px | at one on the ground: a flash step to in front of them and the sword (his first move or his third); with no room, a fan |
| something held on him | 45 ticks of anything kept up by holding the button, from 200 px or further; again every 150 ticks for as long as it is held | the same |
| a reload begun | the mod's magazines | the same |
| a hook of theirs biting | | at one off the ground: his hook; from too near, up beside them; from too far, a fan |

**Measured**: told 600 times of one who stands 300 px off, he answers 397 in his second phase
and 476 in his third.

## 5.13 His third phase, wilder

§16.52, the third of three deliveries of what was asked on the 7th. **Yours**: "His
slashes are more frequent. like he has a move where he leap up, and turn his body and
throw big slashes. Add more anti fly/hook ceiling attacks"; "instead of struggling to
pull it out he plunge it deeper and it creates AOE slashes in a 10 tiles radius circle";
"much faster and wilder and beast like, like maliketh the black blade"; and the answers:
half as fast again, with half a second to a second between moves; delays and feints;
learns you. Every other number is mine. All of it is in `Foigh.Wild.cs`.

**Half as fast again.** The sword's moves on his feet run a tick and a half of their own
clock to the game's tick: the four his second phase quickens, and the hand's sword up and
blow down, the late cut, the mill, the cut up out of the ground, his knives. Not anything
in the air, the hand itself, the hook and its blow, the three cuts let go of, the step
behind. **Measured**, in the game's ticks:

| | as written | second phase | third |
|---|---|---|---|
| his first move | 150 | 121 | 101 |
| his third's blow down | 70 | 57 | 48 |
| the hand: the sword up; the blow and the cut | 16; 42 | 17; 43 | 12; 29 |
| the mill: levelled; three turns | 22; 48 | 23; 49 | 16; 33 |
| the late cut: sunk; past them | 26; 66 | 27; 67 | 19; 45 |
| the cut up out of the ground | 46 | 47 | 32 |
| a knife; three | 30; 70 | 31; 71 | 21; 48 |

His first move's first cut lands 28 ticks after it begins (34; 42 in his first phase).
Its leap back is 9.2 px a tick back and 4.7 up (201 px, 19 ticks in the air); the mill
carries him half as fast again (330 px in its three turns).

**Between moves.** 30 to 60 ticks after a move picked from up close (**yours**). No other
wait is longer than 60, nor, if it asked for 30 or more, shorter than 30. From far off
he walks on at 5 (9.5 in his second phase) and thinks again after 20 to 36 ticks.

**A cut lets go.** The tick a cut of the sword's is over with them ahead of him and more
than 150 px from his middle, a slash of the plain kind (§5.10) goes on at them. No
oftener than every 30 ticks. In: his first move, his third and its turn, his fourth's
turn, the cut after the step behind, the hand's blow and cut, the blow out of the mill,
the cut up out of the ground, the hook's blow. A cut ended for him lets nothing go.

**The wheel** (`Soar`, `Wheel`).

| ticks | |
|---|---|
| 17 | down, as for his fourth move; his eye lights |
| 0 to 24 | up, 15.5 px a tick falling away to nothing: his feet 179 px over the ground (**measured**), and no weight from there |
| all 56 | turning, a turn every 16, the sword out level, his knees up |
| 16, 32, 48 | as the sword comes round to ahead of him, **one of the great cuts** (§5.10: 88 px, 9 px a tick for 100) let go off the middle of the blade at them wherever they are, and straight down at one under him; he looks for them 3 ticks before each, the blade flares 6 before |
| 56 | he falls, and lands as after the blow in the air |

125 ticks from the crouch to walking. The blade cuts nobody while it turns. Begun only
with 130 px clear over his head, at one within 800 px. Weighs 1.2 up close, 2.5 from
further off, 3.5 at one over his head.

**Driven deeper** (the leap that comes down on nobody, in his third phase). Since
§16.53, by request, straight cuts (`FoighLine`), each a blow; §16.52's half circle is gone.

| ticks | |
|---|---|
| 0 to 12 | up over the stuck sword, the far hand come to the handle; the blade flares |
| 24 | driven in, its grip 11 px lower than it stuck at; the ground lit, a red ring out to ten tiles; **the first straight cut is let out** |
| to 63 | thirteen more, at uneven ticks of their own, no two at one |
| each, 12 to 22 after it is let out | it opens, on a tick none of the others does; cuts for 3; hangs 6 to 28 |
| 5 after the last has cut | he tears it out as he did (92 at the latest; it was 44, and stuck for 120 before that) |

Each lies across the ten tiles about the blade at any slant, through a point over the
ground within seven tiles of it, the first and every third through the player (a point of
their body of its own) if they are within the ten tiles; from 45 in a hundred of the line
across the ten tiles there to all of it, wherever along that line, the point on it. Half his
sword's blow and its wound, once; stepped out of, jumped where it lies low, or parried as it
opens. Those at a player reach them while their middle is within the ten tiles (along the
ground, 158 px out); one of the rest may clip a player standing a little beyond.

**Lingers and feints.** Settled as the move begins, by the server, for his first move,
his third, and the hand (as its sword is swung up).

- **Lingers**, 15 in a hundred: the move's clock stands still a tick short of where the
  sword comes down, for 20 to 34 ticks, on every machine; he does not move.
- **A feint**, 8 in a hundred (not the hand): at the tick it would come down he is behind
  them, and the cut after the step behind follows (31 ticks after he is there).
- **The blade flares** as he begins a move that will do either. Where the sword stays up
  it **flares again, and his eye lights, 20 ticks before it comes down** (**measured**:
  the blow lands 25 ticks after that, in all three moves), and he looks for them again as
  its time is up.

**What he learns** (the server's; from his second phase, used in his third). Three
numbers from 0 to 1, each thing seen counting for 15 parts in a hundred: parried (his
sword turned aside, against a move ended that was not); off the ground, and out of his
reach (looked at each time he picks).

| the more they | he picks | at the most |
|---|---|---|
| are off the ground | the blow in the air, his hook, the wheel | 2.5 times as likely |
| keep out of reach | the leap, the late cut, his hook, the three cuts, the wheel, his knives | twice; and walking on half |
| parry | the hand, the cut up out of the ground; and the sword lingers or is a feint | twice; 60 and 35 in a hundred |

One byte more is sent with him (51): how long the sword lingers, or that it is a feint.

## 5.14 Not built

- **The lore plan's ending.** His end is built as your script (§5.17): the sword for Finish,
  nothing for Spare. The plan (PAD_Lore_Planning §9.1) has the sword for sparing him, and
  none, with the achievement Wish Maker, for killing him; Wish Maker is not built.
- **What brings him to the hall.** `/foigh` only (RELEASE_TODO).
- **The fight's first words** (§5.3).
- A flag that he was beaten, a Boss Checklist entry (§2.5), a bestiary entry, a tip.
- **Anything but flat ground.** He does not step up a block or walk a slope.
- **Two machines.** Written for it, never run with two.

## 5.15 The ball, and the parry against his flying cuts

§16.53, asked for on the 8th: "cannot parry wind or blood slashes of the guy"; "if he takes
too much minion damage within a time frame, he curl up into a ball and explode into 20 tile
radius slashes aoe"; "slashes aoe must always be straight cuts"; their own times, lengths
and places; "should not go thru a same point". Every other number is mine.

**The parry.** A parry already begun when one of his flying cuts reaches a player, or one of
his straight cuts opens through them, turns it aside, its wind-up and all (`ParryPlayer.Up`,
`Takes`, `TakesLine`). **Measured**: the blood from the tick it lands to 17 ticks before,
the great one to 19, the wind to 17, one coming down at 45 degrees to 16; a straight cut
from the tick it opens to 13 before. His sword in his hand, his knives and his hook keep
the wind-up first.

**A straight cut** (`FoighLine`): at any slant, through the ground and the air, through
tiles. Shown 12 to 22 ticks (its own), a hair of red brightening; open 3; hanging 6 to 28
(its own). Half his sword's blow through armour, once; the wound in his third phase only.
Its own ticks go with it to every machine (`SendExtraAI`, set in `OnSpawn`).

**The ball**, in all three phases. What fights for a player (`IsMinionOrSentryRelated`) is
counted on their machine over the last three seconds; at nine tenths of all its own cap
lets it take in them (1,620 of 150,000 in a Classic world; since §16.54, when it was given
a cap of its own: before, half of all the cap would let him lose in them, 1,800) the server
is told (`Swarmed`), and as soon as he is free he answers. No oftener than every ten
seconds.

| ticks | |
|---|---|
| 0 to 8 | down, facing them |
| 8 to 24 | up, 9 px a tick falling away to nothing (**measured**: 77 px), folding up; then no weight |
| to 52 | curled up, shaking, **nothing landing on him**; the red drawn in (in his first two phases air, white: §5.17), and from 8 a roar drawn in to him; the blade flares (gleams, white) and his eye lights 20 before the end; he looks for them 3 before |
| 30 to 51 | sixteen straight cuts let out, at uneven ticks, no two at one |
| 52 | he bursts: a flash, a ring of red (of steel, white) out as far as the cuts reach, the ground shaking; the first cut opens |
| 52 to 73 | the rest open, each on a tick of its own |
| 58 on | he falls, and lands as after the wheel |

Each across the tiles about him that his phase has it reach (18, 23 and 27: §5.17; twenty
before §16.55) at any slant, through a point of its own within three fifths of that of him;
the first and every fourth through the player it is for (a point of their body of its own)
if they are within its reach. Each cuts down what it lies through of what fights for a
player, four, six and nine times in nine by phase (§5.16, §5.17).

## 5.16 What fights for a player; and the blood of Ultra Bleeding

§16.54, asked for on the 8th: "minion: 4/9 chance for 3"; "the other applies" (§16.53's
other answers: turned aside at his usual rate, a cap of its own, one cooldown for the whole
Stardust Dragon); "killing 1 stardust dragon means killing the whole dragon"; and
"ultrableeding have blood particle spewing down". Every other number is mine. In
`Foigh.Called.cs`, the ball's cuts in `FoighLine.cs`, the blood in `UltraBleeding.cs`.

| | Classic | Expert | Master |
|---|---|---|---|
| what lands of it in any sixty ticks, at the most (**yours**: half of the cap's second) | 600 | 960 | 1,440 |
| the ball, at what lands of it in three seconds (mine: nine tenths of that cap's three seconds) | 1,620 | 2,592 | 3,888 |

Half as much again in Legends.

**Turned aside** (**yours**: two in three, four in five in his third phase): in all three
phases, and in whatever move he is in, as what keeps coming is (mine); not while he answers
nothing (broken, down on a knee, his sword thrown aside). A roll each time the game asks
whether a hit of it lands, which is once for each hit it would make. A minion turned aside
is not hurt, and is through with him for as long as a hit would be; a shot of it that stops
at what it hits is struck out of the air. It never wears his posture, never counts toward a
stream, and never holds him at it (mine). With nothing more to be taken from him, or by it,
every time. **Measured** over 500 asked of a minion on him: 171 landed in his first phase,
184 in his second, 108 in his third.

**Its own cap**: worked out on the machine of whoever's it is, where the hit is, and counted
for each player apart. A hit is cut down to what is left; with less than a point left it is
turned aside; broken, a point gets through.

**The Stardust Dragon**: one wait for all of it, its head's (the game's own, 1.4.4); turned
aside, the whole of it waits.

**The ball's cuts**: each, open, cuts down what it lies through of what fights for a player,
four in nine (**yours**; since §16.55 by phase, §5.17), each tried once by each cut: minions
and sentries (mine), not what they shoot; not the cuts out of the ground (mine); any part of
the Stardust Dragon, the whole of it (**yours**).

**Ultra Bleeding's blood** (**yours**: blood, spewing, down): each wound spews from a place
of its own (the chest, the side behind, the hip, turned with the way they face), a spurt
every beat (36 ticks: five ticks of two drops), a run of it between (one tick in three), the
wounds a third of a beat apart; every drop down to the ground under them and no further, and
a spurt's drop that comes down leaves a stain one time in three (150 to 230 ticks). Every
machine sees it.

## 5.17 His end; and the ball by phase

§16.55, asked for on the 8th: his death, as a script (PADWorkFlow §16.55 has it word for
word), and the ball by phase: white in his first two phases and red and black in his third,
across 18, 23 and 27 tiles, cutting down what fights for a player four, six and nine times
in nine ("normal", "1.5x", "3x": twelve in nine is every time), with a roar drawn in to him,
red and black, before it; his own roar red and black too. Every other number is mine. In
`Foigh.End.cs`, with the player's side in `FoighPlayers.cs` (`FoighEndPlayer`), the poses in
`FoighMoves.cs`, the hall's flames in `EmptyHall.cs`, the ash in `AshDrift.cs`; the ball in
`Foigh.Wild.cs` and `FoighLine.cs`.

**Begun** by the blow that would kill him (he is left on one life, nothing lands on him from
then) or by `/foigh end`. In the hall (`EmptyHall`) he goes to the fourth pilaster from its
end wall (the half one against the wall the first: column 179), facing down the hall;
anywhere else he stays where he is. Only the player he fights is held, shown and asked; to
anybody else he is simply doing it.

| part | ticks | |
|---|---|---|
| Dying | 40 at least | rocked by the blow; on the ground, gone to the pilaster (a ghost left where he stood) |
| Roar | 208 | a breath to 22; the voice to 172 (the game's roar at -0.45, one and a half times a boss's, rimmed red); the hall's flames out from him at 40 px a tick but the five up his pilaster |
| Fold | 280 | to black 24, the player put down twenty tiles in front of him at 27, back by 70; the camera over to him from 82 (96 ticks, leaning to 1.3); swaying to 150, his knees by 196, the sword let go at 216, his hands down by 234, the sword down by 244 |
| Bowed | the walk | the player walked up to five tiles off him; his head hanging, shaking, blood from his mouth (fifteen seconds at the most) |
| Laugh | his line | his head up (36); "Ha ha ha ha..."; two to four coughs; "..." |
| Asked | the answer | "It is true after all, ..." (Yes you were) (No you weren't) |
| Knew / Mouth | the answer | "I knew it", the coughs, "300 years of training", "..." / "I did get you to bleed...", "..." |
| Choir | the answer | "O, Guah! ...", "..." |
| Plead | the choice | up on his knees (60), back straight, head up at the sky, hands down; "Please... chosen one. ..." (Finish) (Spare) |
| Finish | the walk | the sword taken up (30), carried on the shoulder, the walk up to his shoulder |
| Thrust | "Thank you" | the hand on his shoulder (12), the sword through him thirty degrees down (22), rigid to 96, looking at them by 126, "Thank you" at 132 |
| Cast | 432 | the blade out (18), the shove (26), on his back (50); smoke from 58; ash from 158, a heap, blown away from the player; to black at 358, the sword given and the flames lit at 386, back by 432 |
| Spare | 700 | the sword taken up and thrown behind the player at 62 (about fifteen tiles); his head down from 100; the walk away from 110; ten seconds |
| Scorn | his line | "What insolent moron..." |
| Slump | 34, then out | down on his face; the passage out of the place (anywhere else, black and back, 108 ticks), and gone |

**Waiting**: every line and walk waits for the player's machine (`Word.Last`, with the
part), and not for ever: a line ninety seconds, the question then "No you weren't", the
plea's choice then Spare. Nobody left to see it: down on his face, and gone.

**The player**, for all of it: held (or walked, at 1.6 px a tick, whatever is on their
feet), nothing in use and nothing built, nothing able to touch them, no life lost by itself,
his wounds of Ultra Bleeding stopped as it begins; their arms and his sword in their hands
drawn on every machine.

**The flames**: a torch out is its frames' second half, a Tiki Torch out its second column,
as the game's wires switch them; lit again under the last black of Finish, when he is gone
however it ends, and whenever the hall is loaded (`EmptyHallFlames`).

**What it leaves**: Finish gives Foigh's Rust Side Sword (`FoighsRustSideSword`: since
§16.57 a weapon of its own, 600 through all defense, PAD_Item_Plan; its picture still a copy
of `SwordsmanBluntSide.png`); Spare gives nothing. Nothing else is dropped or said. The lore
plan (PAD_Lore_Planning §9.1) has the sword for sparing him and none, with the achievement
Wish Maker, for killing him: built as the script; Wish Maker is not built.

**The ball by phase** (**yours**, but the points it goes through, which are mine):

| his phase | the ball | reach | the points within | cut down, of nine |
|---|---|---|---|---|
| first | white | 18 tiles, 288 px | 173 px | 4 |
| second | white | 23 tiles, 368 px | 221 px | 6 |
| third | red and black | 27 tiles, 432 px | 259 px | 9 |

White: the air drawn in with a white light, a gleam on the blade, a white flash and a ring
of steel, the cuts a hair of steel then open white steel, sparks of steel. Before it bursts,
in every phase, the roar drawn in: rings of black rimmed red come in from off the screen and
are gone into him as he bursts (44 ticks from his leap), silent.

Rimmed, as both his roars are: each ring's fainter band red and its dark band black, as thin
and as clear as any boss's black roar (§16.56: §16.55 drew the red over the whole square of
each ring).
