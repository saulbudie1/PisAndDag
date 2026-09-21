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
