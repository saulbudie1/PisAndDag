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
| **Foigh, the Swordsman** | `Content/NPCs/Foigh` | **his road and phase one of three built; put down by `/foigh` only; §5** |

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

**His road and the first of his three phases are built** (PADWorkFlow §16.43). Phases two
and three, the ending, and whatever brings him to the hall are not: §5.8.

Everything here was run in a rig on the game's own engine (its NPC update with tile
collision, its projectile update, `Player.Hurt`, an item's swing) and **never seen in
play**. Numbers marked **yours** are as given; **measured** came out of that rig; the rest
are mine and want confirming.

## 5.1 What is built

| | |
|---|---|
| `Foigh.cs` | the NPC: numbers, moves, what is sent with him, the words a player's machine sends the server, the one tick of his AI, `/foigh`'s call |
| `Foigh.Scene.cs` | the road: his lines, its ticks, what the player it is for is shown |
| `Foigh.Fight.cs` | the fight: every move, what he picks, the sword against the player |
| `Foigh.React.cs` | what he does about what is sent at him; `FoighMark`, a shot's verdict kept on the shot |
| `Foigh.Draw.cs` | drawing him from his pieces, the blade's trail, the ghosts of a flash step, sparks, dust, sounds |
| `FoighRig.cs` | where everything is on his art, and the sums that put seven pieces together for any pose. No game in it |
| `FoighTrack.cs` | a movement as poses at moments, eased between |
| `FoighMoves.cs` | every movement he has, and the ticks things happen at in each |
| `FoighPlayers.cs` | the player's side: the walk up (`FoighScenePlayer`); held, thrown or shoved (`FoighGripPlayer`) |
| `FoighDagger.cs`, `.png` | his throwing knife (the picture is a stand-in: PlaceholderTextures.md) |
| `FoighSwordThrown.cs` | his sword, thrown, and standing in the ground |
| `FoighReturned.cs` | a shot of a player's on its way back to them |
| `FoighCommand.cs` | `/foigh`, `/foigh gone`, mod root |

He is **seven pictures, not a sheet**, each turned about its own joint: your head, torso,
two arms, two legs and sword, used as they are. A movement is poses at moments; the fight
says which movement and which tick; the drawing, the blade that hits and the trail behind
it all ask the same sum. If a piece is exported again at another size, its numbers at the
head of `FoighRig.cs` are what has to change (size, the joint on the picture, the limb's
length).

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

All of it through armour (all of a player's defence ignored), varied as the game varies any
hit. Defence 40 (mine). Not grown by how many are in the world. Stages by his life: over
66%, to over 30%, from 30% down (**yours**); only the sword's damage follows them yet.

How far a blow throws a player standing still (**measured**): a cut about 7 tiles, the
thrust 13, the blow down 12, a knife 4, a swing of theirs met 10, the thrown sword 13, the
fling off the sword 28.

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
| **two** | down into his knees (18), a leap onto them, point first, aimed a third of the way ahead of where they are going (34 to 56 ticks in the air, up to 30 tiles). On them: 12 ticks, then 3 in 5 into the first move from its second cut, 2 in 5 into twice round. On nothing: the sword is in the ground **two seconds** (**yours**), and 34 ticks more to be back in his stance | |
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
| a minion, a sentry, and what those shoot | never | lands |

**Measured** over 400 of each: bullets 178 struck and 85 sent back, arrows 154 and 71,
throwing knives 167 and 100, a spell 239 stepped out of the way of, rockets 244, swings
235 met.

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
leap, the third move's blow and turn, twice round, the cut from behind. Not the thrown
sword, not the hand. He reels 44 ticks; on every third he is down on a knee two seconds,
open to the riposte (three times the blow). A knife and a shot sent back are broken by a
parry.

## 5.8 Not built

- **Phases two and three.** Nothing happens at 66% or at 30% but the sword's damage.
- **The ending.** A blow that would kill him leaves him on 1: a knee, a fade, gone, nothing
  dropped, nothing said. The lore plan's choice (kill him as he asks, or spare him and be
  given his sword) belongs here.
- **What brings him to the hall.** `/foigh` only (RELEASE_TODO).
- **The fight's first words** (§5.3).
- A flag that he was beaten, a Boss Checklist entry (§2.5), a bestiary entry, a tip.
- **Anything but flat ground.** He does not step up a block or walk a slope.
- **Two machines.** Written for it, never run with two.
