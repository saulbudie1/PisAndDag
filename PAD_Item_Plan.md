# PisAndDag — Item Plan

> Every item the mod adds, in three parts: **weapons**, **consumables**, **lore items**.
> Lore lives in `Terraria_Mod_Lore_Bible_ver_1.1.md`; this file is the machine.
>
> `build.txt` has no `modReferences`. Everything here is balanced against **vanilla** and
> only sanity-checked against Calamity. Nothing may depend on Calamity loading.
>
> Replaces `PAD_Greatbow_Plan.md`, which should be deleted.

---

# 1 · Weapons

`Content/Items/Melee`, `Content/Items/Ranged`, `Content/Items/Summoner`

| item | class | state |
|---|---|---|
| Blade of Mors | Melee | built |
| Dented Megashark | Ranged | built |
| Chlorophyte Rifle | Ranged | built |
| Handswitch | Ranged | built |
| **Greatbow of a Pilgrim** | Ranged | **textures in, code to write** |
| Iron Flow | Melee | built; never run in game (below) |
| Meat Hook | Ranged | built; never run in game (below) |
| Foigh's Rust Side Sword | Melee | built; run once in the game (below); a lore item too (§3) |
| Hoard of Undead | Summoner | built; never run in game (below) |
| Bone Squad | Summoner | built; never run in game (below) |
| Wooden Crossbow | Ranged | built; never run in game (below) |
| Mycellaneous | Ranged | built; never run in game (below) |
| Worminator | Summoner | built; never run in game (below) |
| Vulture Staff | Summoner | built; never run in game; nothing gives it yet (below) |

---

## Greatbow of a Pilgrim

Handed over by Pugream after Armageddon. Lore: Bible §XXXI *The Greatbow*, §IX *What He
Spends*. The player watched this weapon work, once, in somebody else's hands, before it
broke.

### Files

| | |
|---|---|
| item | `Content/Items/Ranged/GreatbowOfAPilgrim.cs` + `.png` ✔ |
| holdout | `Content/Projectiles/Ranged/GreatbowOfAPilgrimHoldout.cs` + `.png` ✔ 20×64 |
| string | `Content/Projectiles/Ranged/GreatbowOfAPilgrimString.png` ✔ 20×64 |
| ammo | `Content/Items/Ranged/GreatArrow.cs` + `.png` — §2 |

**One projectile, two layers.** A projectile type has exactly one
`TextureAssets.Projectile[type]`, so the string is **not** a second projectile. The holdout
draws both in `PreDraw`, requesting the string by path. One object, one netcode entity,
nothing to keep in sync. Both textures share the 20×64 canvas so they overlay at the same
origin.

### The string does the whole animation

**The stave never moves.** One shader, not two.

It used to bend forward, lose height and widen, all driven by the draw counter, and all of
it came out after watching Heavenly Gale — which never deforms its bow at all. A stave that
moves competes with the string for the eye, and the string is the part carrying the
information. Rigid wood reads as rigid wood, and the V in front of it does the talking.

So the animation is two straight runs from the nocks to a point that travels back as the
counter climbs, drawn between three positions in `PreDraw`. Nothing about it is a texture
warp, so the pull is limited by nothing.

`Effects/GreatbowString.fx` is all that is left, and it does what geometry cannot: pinches
the run toward the apex so the two meet in a spike rather than two ribbons crossing,
thins and brightens the whole run as the bow comes back, and puts a shimmer along it,
because the string is hair from an animal that does not live in this land.

Draw it in `PreDraw` with `spriteBatch.End()`, re-`Begin` in `SpriteSortMode.Immediate` with
the mod's own `MiscShaderData` applied, and restore the batch afterwards. The mod already
does custom `.fx` (`Effects/MorsCloak.fx`) — follow that loading pattern.

**`Effects/GreatbowLimb.fx` and its `.xnb` are dead files.** Delete them.

### The hands

`SetCompositeArmFront` / `SetCompositeArmBack`, the Heavenly Gale way. The back arm points
along the aim, a quarter turn off because the composite arm measures from straight down.
The front arm's angle falls out of the string: half of it is a right triangle, nock-to-centre
height against the pull, so the hand sits at `atan(half / pull)`. At rest the arm lies along
the bow; at full draw it is back by the ear. It tracks the counter for free, because it reads
the same two numbers the string is drawn from.

**Flip handling.** Aiming left, a plain half turn carries the belly round but stands the
stave on its head. Turn it **and** mirror it — `rotation += Pi` with `FlipHorizontally` when
`cos(rotation) < 0` — and it stays upright either way. The string is built from points, not
a sprite, so it uses the unflipped rotation.

### The draw

`Item.channel = true`, `Item.shoot` = the holdout, `CanShoot => false`. The holdout owns
the counter, the shaders, the meter and the release.

| held | damage | lodge hits | stagger | push back | flight |
|---|---|---|---|---|---|
| < 0.5 s | — | — | — | — | **cannot release** |
| 0.5 – 1 s | **350** (1.0×) | 1 | none | none | curved and flaccid; it droops |
| 1 – 3 s | 350 → **525** (→1.5×) | 5 | ≤ 1 s | none | like a Chlorophyte Shotbow |
| 3 – 5.5 s | 525 → **1050** (1.5→3×) | 7 | 2 – 4 s | **2 tiles** | flatter, still falls |
| 5.5 – 6 s | holds at 3× | 7 | 2 – 4 s | 2 tiles | — |
| ≥ 6 s | **1400** (4×) | 10 | **5 s** | **10 tiles** | dead straight, endless, fast |

`Item.damage = 350`. Everything above is a multiplier on it, computed at release and
carried into the projectile.

**The half second that does nothing.** 5.5 to 6 is a dead band: damage stops climbing, then
snaps to 4× on the sixth second. Keep it — it is the weapon refusing to be smooth — but it
must be **marked on the meter**, or it reads as a bug rather than a gate.

**The missing second.** Stagger at full draw is 5 s; the draw is 6 s. The player is always
one second short of being ready when the thing gets up. His bow drew in five. That gap is
the wound *and* the balance: a 5 s stagger on a 5 s draw is a permanent lock on every boss
in the game. Do not close it. (Bible §XXXI carries this.)

### The arrow

Same shape as `Content/Projectiles/Melee/BladeOfMorsProj.cs` — read that file first.

```csharp
Projectile.penetrate = -1;
Projectile.tileCollide = false;
Projectile.usesLocalNPCImmunity = true;
// set per shot, NOT a constant: cooldown = staggerTicks / hitsPerLodge
```

On hit: stash the velocity, zero it, run a lodge timer, resume — the `FreezeTimer` /
`storedVelocity` pattern. **The lodge lasts exactly as long as that tier's stagger**, so it
bites, hangs in the thing for the whole stagger, tears free and carries on to whatever is
behind. One object, two effects, nothing to explain.

Cooldown falls out of the tier:

| tier | stagger | hits | `localNPCHitCooldown` | per shot | DPS |
|---|---|---|---|---|---|
| 1 – 3 s | 60 t | 5 | 12 | 2 625 | ~875 |
| 3 – 5.5 s | 120–240 t | 7 | 17–34 | 7 350 | ~1 336 |
| ≥ 6 s | 300 t | 10 | 30 | **14 000** | **~2 333** |

Vanilla's best single-target ranged sits under a thousand, so the full draw is roughly
2.5× anything else in a vanilla world. That is the intended shape — a six-second
commitment with nothing else to do — but it is the number to cut first if the fights stop
being fights.

### Stagger

Terraria has no stagger. `GlobalNPC` with `InstancePerEntity`:

- a `StaggerTicks` field on the instance;
- `PreAI(NPC npc)` returns **false** while it is above zero, and zeroes `npc.velocity` in
  the same call;
- decrement in `PostAI` so it keeps ticking while AI is skipped.

`GlobalNPC.PreAI`, `AI`, `ModifyIncomingHit` and `OnHitByProjectile` are all verified
present in the staged `tModLoader.dll`.

**Risk.** Skipping AI breaks bosses whose state machines assume they run every tick —
teleporters mid-teleport, multi-segment worms, anything with a despawn gated on a counter
in `npc.ai[]`. Expect a **blacklist**. Test worms first.

### Push back

2 tiles (32 px) at the middle tier, 10 tiles (160 px) at full, **uniformly and lightly** —
the same distance whatever it is, delivered as a slide rather than a punt.

Do it by writing position directly. Not `knockBack`: every boss in the game resists that,
and "even the Wall of Flesh goes backwards" is the whole point of the line.

**The Wall of Flesh** drives its own X from its AI and accelerates as it loses health, so
the push has to survive the Wall re-deriving position on the next tick. Check that it
sticks instead of snapping, and that both eyes and The Hungry follow it.

### The skyward rain

Right-click. He points it up, **charges 8 seconds**, and 10–15 Great Arrows come down on a
point **locked before the charge starts**.

- The point is fixed at the moment of commitment. Everything lands there.
- Each arrow staggers and applies bleed.
- No cancel, no refund. Eight seconds is the cost and it must feel like one.
- Draw a marker on the locked point for the whole charge, so the player can watch the
  target walk out of the box they just paid for.

It costs about twelve shots of ammunition and returns about one shot of damage. **That is
correct.** The rain is a control tool, not a damage rotation. Anyone who later "fixes" its
damage has broken it.

### Open

- Dead-band marking on the meter (§ the draw).
- Whether repeated staggers on one NPC diminish — 5 / 3 / 2 s with a 1 s floor, resetting
  after ~15 s — or stay flat and this bow is the answer to every boss.
- Bleed: vanilla debuff or a PAD one.
- Where Great Arrows come from.

---

## Iron Flow

A hooked sword with a crescent for a guard. It comes from the Blind Tusker, one in four
(PADWorkFlow §16.75); its recipe, 12 Iron or Lead Bars at an anvil, is commented out. 25
damage. Your art (`Content/Items/Melee/IronFlow.png`, 64 by 64, the point up and to the right).
The whole record is PADWorkFlow §16.26, and §16.30, §16.31, §16.33, §16.34 and §16.42 for the
parry as it is now.

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Melee/IronFlow.cs` | `IronFlow` | The item. Hits nothing itself; tells the sword in the hand what to do |
| `Content/Projectiles/Melee/IronFlowHoldout.cs` | `IronFlowHoldout`, `IronFlowPose`, `IronFlowLayer` | The sword in the hand while it is the item held: the weapon |
| `Content/Projectiles/Melee/IronFlowMoves.cs` | `IronFlowMoves`, `IronFlowState` | Every pose of every move, as curves; nothing of the game in it |
| `Content/Combat/Parry.cs`, `ParryTiming.cs` | `Parry`, `ParryPlayer`, `ParrySystem`, `ParryTiming` | The parry: not this weapon's, any weapon's |
| `Content/Combat/ParryRules.cs` | `ParryRules`, `ParryClass`, `ParryWhy` | What can be parried, read off the thing itself |
| `Content/NPCs/PADStaggerNPC.cs` | `PADStaggerNPC` | The stagger a parry gives, the push, the riposte (and the Greatbow's stagger, as it was) |
| `Content/NPCs/GolemFistParry.cs` | `GolemFistParry` | The Golem's fists parried: sent back into the Golem |
| `Content/NPCs/GolemPace.cs` | `GolemPace` | While its head is on, the Golem and its fists keep a full-life Golem's pace |
| `Content/NPCs/SkeletronHandParry.cs` | `SkeletronHandParry` | Skeletron's hands parried: a fifth of a hand's full life, thrown back |
| `Content/Projectiles/NPCs/QueenSlimeCore.cs` | `QueenSlimeCore` | The Queen Slime's core in Legends, parried: sent back into her |
| `Content/Combat/ParryEffects.cs`, `Content/Particles/ParryRing.cs` | `ParryEffects`, `ParryRing` | What it looks and sounds like |
| `Content/Combat/ParryConfig.cs`, `ParryCommand.cs` | `ParryConfig`, `ParryCommand` | Five lists for whoever runs the world; `/parry` |

### In the hand

Held and not in use, it hangs: the hand at the hip, the blade pointing down and ahead, the hook
by the feet.

**Left click**, for as long as it is held: three cuts in turn, each from where the last left the
blade, each carrying you forward.

| Cut | Ticks | Carries you |
|---|---|---|
| Down: raised over the shoulder, brought down | 24 | 1 block |
| Up: from where it hangs, up and over the shoulder | 24 | half a block |
| Round: a turn and a half, down through the front, under, over, and down again | 36 | 1.5 blocks |

Left more than 40 ticks, or parried between, they start over.

**Right click** parries. A fresh press every time. 42 ticks whatever your speed: 6 before it does
anything, 8 in which it does, 28 open (Elden Ring's Parry on a curved sword: 3, 4 and 14 frames
at 30 a second). The blade goes once round the other way, up through the front.

### The parry

For the 8 ticks there is an arc in front of you that nothing draws: 48 pixels from your middle,
from just behind overhead, round the front, to just behind straight below. What can be parried
and is inside it, or comes inside it, in those ticks is turned aside; so is anything that
reaches you from the front in them. Nothing from behind. An enemy has to be attacking: able to
hit you, and coming at you.

**Shots.** Solid things shot or thrown, when they are an enemy's: arrows, javelins, bones,
knives, stingers, spikes, seeds, darts. Bullets: vanilla's enemies' three; any other only if it
stops at the first thing it hits (`penetrate` 1). Not light, fire, magic or spit. A parried
shot breaks.

**Enemies**, by what they are at that moment (`ParryRules`):

| Class | Which | A parry |
|---|---|---|
| Light | the game lets it be knocked back | turns it aside, strikes it, pushes it back; staggers it every second parry if it flies, every third if not |
| Heavy | the game does not | the same; only the riposte's knockback differs, which is the game's |
| Giant | wider than 60 px or taller than 84 | its hit lands; the parry's blow lands on it too, once |
| Never | a boss or a part of one, a shot in an enemy's place, a bomb, a ghost, beyond being hurt, under the yellow star | nothing |

In vanilla: 353 light, 62 heavy (35 of them worms; 3 the mod speaks for, the Golem's two fists and Skeletron's hand, never staggered), 25 giants, 74 never.

- **The blow** is the damage of the weapon that parried, as you have it: no critical hit, no
  variation, no knockback of its own.
- **The push** is 96 pixels, for every enemy turned aside, whether or not the game lets it be
  knocked back; tiles stop it.
- **The stagger** is 2 seconds, on every second parry of an enemy that flies (no weight at that
  parry) and every third of any other; the count never runs down and starts over at each
  stagger. Its AI is stopped and goes on after from where it was; it can hit nobody; a parry
  does nothing more to it.
- **The riposte** is the first hit on a staggered enemy: 3 times its damage, 4 times its
  knockback, never a critical hit, and it wakes at once.
- A flying weapon (the Enchanted Sword, the Cursed Hammer, the Crimson Axe) is staggered its
  own way, on every second parry: thrown back spinning for 2 seconds, open to the riposte
  while it spins.

**'Dame' Quilt** can be parried in three of her four attacks: the charge, the thrown lance,
Triunfo running you down. Not the swing. Every parry strikes her and throws
Triunfo back six tiles; every third one staggers her for 2 seconds (she counts as on the ground). The thrown lance is
knocked away and comes down somewhere else.

**Amalgaermis**: its bones and skulls break on a parry. Its hands and its ash do not.

**The Golem's fists** can be parried in the punch, though they are parts of a boss. A parried
fist takes the blow, turns about and flies back into the Golem; when it gets there it loses
a seventh of its full life and the Golem a twenty-first of its own, head on or off. Never
staggered, never pushed. Seven parries break a fist. While the head is on, the life that
costs the Golem does not make it or its fists any faster; once the head is off it does.

**Skeletron's hands** can be parried while they are swung at you, though they are parts of a
boss: vanilla's swing down and its sweep in from his side (and for as long after as the hand
is still flying), and in Legends the clap as the hands shut and the smash as they come down.
Not a hand that only hangs by him. A parried hand takes the blow, then loses a fifth of its
own full life whatever its armour, is thrown back about nine tiles, and touches nobody for 45
ticks. Never staggered, never pushed. Five parries break a hand. In the clap and the smash
the other hand is thrown back with it and loses nothing, neither can touch you, and the bang
of the blow is left out.

**The Queen Slime's core**, thrown in Legends, can be parried anywhere on its way out or back.
It is not broken: it turns and flies straight back into her, faster each tick, hurting nobody,
and as it goes in she loses a tenth of her full life whatever her armour. Then she catches it.
The tenth can bring her wings, or begin her death.

**For other mods and other worlds**: the config (five lists), `Mod.Call` with `"ParryNPC"`,
`"ParryNoNPC"`, `"ParryNoStagger"`, `"ParryProjectile"` or `"ParryNoProjectile"` and a type,
`/parry` and `/parry list`. In code: `Parry.AddNPC(type, when, thrown, flies, reels, told)`,
`Parry.AddProjectile(type, when, turned)`, and for a hit the mod works out itself,
`ParryPlayer.Spares`, `MeetsLine`, `MeetsBox` and `Takes`. `Parry.Take` is a flat loss of
life, armour or no; `ParryPlayer.AboutToTurn` is for a blow that is two things' as one.

Pressed 6 to 17 ticks before an arrow at 11 pixels a tick would land, it is parried: a fifth of
a second. Later and it lands first; earlier and it lands after.

### Open

- Any weapon or shield can parry, each with its own ticks (buckler 6, 12, 24; dagger 6, 10, 26;
  small shield 8, 12, 22: Elden Ring's, doubled), and a key for the ones whose right click is
  taken. `ParryPlayer.Begin(timing, facing, weapon)` is there for it; nothing but the Iron Flow
  calls it.
- The numbers that are mine, and the readings to confirm: RELEASE_TODO.

---

## Meat Hook

A double-barrelled shotgun with a hook on a chain slung under its barrels. Your art, four
textures (below). No source yet: a recipe is written out and commented in the item. The whole
record is PADWorkFlow §16.41.

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Ranged/MeatHook.cs` + `.png` (70 by 22, yours) | `MeatHook` | The item: the shell fired |
| `Content/Projectiles/Ranged/MeatHookHoldOut.cs` + `MeatHookHoldOut_WithoutHook.png` (70 by 22, yours) | `MeatHookHoldOut`, `MeatHookLayer` | The gun in the hands while it is the item held: aims, kicks, breaks open, throws the hook; drawn in the hand |
| `Content/Projectiles/Ranged/MeatHook_Hook.cs` + `.png` (22 by 32, yours), `MeatHook_Chain.png` (14 by 16, yours) | `MeatHook_Hook` | The hook and its chain |
| `Content/Combat/MeatHookPull.cs` | `MeatHookRules`, `MeatHookNPC`, `MeatHookPlayer`, `MeatHookHUD` | Who is dragged, the two drags, the wait, the bar beside the player |
| `Content/Combat/ParryRules.cs` | `ParryRules.BossOrPart` | A boss or a part of one (one line, beside the parry's lists) |

The hook as it lies on the gun has no texture of its own: it is the item's sprite less every
pixel of the gun without it, worked out once (the two are the same gun, pixel for pixel).

### The gun

**Left click** fires a shell: 8 of whatever bullet is loaded for the one that is spent, 22
damage each, two of them near the line and the rest within 9 degrees of it. 28 ticks a shot; 2
shells, then 72 ticks to reload (PSItem's magazine, as every gun's; the Reload key does it
sooner). It is in both hands whenever it is held, pointed at the cursor; it kicks with a shot
and is broken open for the reload.

### The hook

**Right click** throws it, a fresh press each time; while the gun fires, reloads or is empty
too. One at a time.

| Stage | What it does |
|---|---|
| Out | 24 px a tick, the harpoon's sound; 50 tiles at the most; a tile turns it back; through platforms |
| Caught | the first enemy it touches takes 1.5 of the gun's damage and is held: 5 ticks of bite, the drag, then 48 ticks more; 150 at the most |
| Back | straight to the gun through anything; there, the wait begins: 3 seconds if it caught something, 5 if not |

**A small enemy is dragged to you; anything else drags you to it.** Small
(`MeatHookRules.Hauled`): the game lets it be knocked back, it is no wider than 60 px and no
taller than 84, it is no boss and no part of one, and its AI can be stopped. In vanilla 378
kinds are dragged in and 164 drag you: bosses and their parts, worms, giants, and what cannot
be knocked back (a Paladin, a Target Dummy). Every NPC of the mod's own drags you.

- **Dragged in**, an enemy's AI is stopped and it touches nobody; it comes at 20 px a tick,
  over ledges, and hangs 30 px off. Let down, it is thrown a little way off and goes on from
  where it stopped.
- **Dragged out**, you go at 16 px a tick and stop 56 px short, with no fall counted, your
  jumps back, off a mount and off any grappling hook.
- **While the hook is in an enemy every pellet does double damage**, at whatever it hits. "x2"
  stands beside you.
- **It lets go** when what it holds dies, when what it drags comes no nearer for 24 ticks, with
  the chain past 1.15 of its reach, on a second right click (once it has held 12 ticks), on a
  jump when it is you being dragged, and when you are barred from using anything. The gun put
  away, or you dead, frozen, stoned or webbed, it is gone at once.
- **What shows it**: the hook glows red on the gun when it can be thrown, lies dark while it
  waits and is not there while it is out; a small bar beside you fills as the wait runs out; a
  low note when it is over.

### Open

- No source: the recipe in `MeatHook.cs` is commented out (a Shotgun, a Grappling Hook, 10
  Chain, 10 Souls of Night, a Mythril Anvil).
- Letting go (a second right click, a jump) is said nowhere in the game.
- Never run in the game, never heard, never tried with a second player.
- The numbers that are mine, the readings to confirm and what was added without being asked:
  RELEASE_TODO.

---

## Foigh's Rust Side Sword

What finishing Foigh gives (PAD_NPC_Plan, his end): his sword's rusted side, a weapon of its
own. 600 melee damage through all defense, used again for as long as the button is held. Its
picture is still a stand-in, a copy of your `SwordsmanBluntSide.png` (PlaceholderTextures.md).
No source but Finish. Its rarity is the mod's own, Blasphemous Crimson (below, The mod's own
rarities). The whole record is PADWorkFlow §16.57 to §16.59, and §16.61 for its rarity
(§16.55 for the stand-in it was).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Melee/FoighsRustSideSword.cs` + `.png` (90 by 90, a copy of yours) | `FoighsRustSideSword` | The item. Hits nothing itself; tells the sword in the hand what to do, and is in use exactly as long as that takes |
| `Content/Projectiles/Melee/RustSideHoldout.cs` | `RustSideHoldout`, `RustSidePose`, `RustSideLayer` | The sword in the hand while it is the item held: the weapon. The cuts, the air, the leap and the drive, the focus, what it hits, how it looks and sounds; drawn in the hand |
| `Content/Projectiles/Melee/RustSideMoves.cs` | `RustSideMoves`, `RustSideState` | Every pose of every move, as curves; nothing of the game in it |
| `Content/Projectiles/Melee/RustSideSlash.cs` | `RustSideSlash` | A slash let go of in the air |
| `Content/Projectiles/Melee/RustSideLine.cs` | `RustSideLine` | A straight cut out of the ground, from the drive |
| `Content/Buffs/BloodDrenchedSword.cs` + `.png`, `DriedBlood.png` | `BloodDrenchedSword`, `DriedBlood`, `BloodDrenchedPlayer` | The blood, its wait, and Enter |
| `Content/Buffs/UltraBleedingGeneric.cs` (its picture `UltraBleeding3.png`) | `UltraBleedingGeneric`, `UltraBleedingNPC` | Ultra Bleeding on an enemy: the life it takes, the blood out of it |
| `Content/NPCs/Foigh/SwordsmanBloodSide.png` (yours) | | The sword while it is drenched |

### In the hand

Held and not in use, it lies on the shoulder: the hand before the chest, the blade up and back
over it.

**Left click**, for as long as it is held: five cuts in turn, each from where the last left the
blade, each carrying you so far (exactly, standing still; added to a walk or a run).

| Cut | Ticks | Carries you |
|---|---|---|
| Up: drawn back and down behind, up through the front to over the head | 22 | 1 tile |
| Down: down through the front to low before the feet | 22 | half a tile |
| Round: a turn and a quarter, the arm along the blade, left trailing behind | 33 | none |
| The smash: raised behind the head in both hands, held, brought down into the ground | 35 | 1.5 tiles |
| The thrust: drawn back to the hip, driven straight ahead | 28 | 2 tiles |

Each cut hits each enemy once. Left more than 40 ticks, the chain starts over. The ticks are at
the item's own speed; faster, all of them shorter.

**The aim**: with an enemy within eight tiles that you can see, each cut is made at it (of
several, the one nearest the cursor). You face it; under it or in it, the cursor's way still. A
step stops six pixels short of it, so you are never carried into it, past it or out from under
it: under a boss, not at all. The thrust is turned at the nearest of it, straight up at a boss
over your head. With nobody near, the cursor's way, each step whole.

**Right click**, held or pressed again: up to three in the air, then nothing until the ground.
Up off the ground, a roll forward and a cut at the cursor that lets a slash go; a little higher,
another roll and a rising cut; two rolls and a cut from far back, the great slash. They hang
while they cut. The blade does no damage in the air; the slashes do: 1.5 times the sword's
damage, 1.5 and 3.

**A slash** goes straight on through everything, tiles too, for 2.5 seconds at the most, hitting
each enemy again every 7 ticks while it is in it: 76 px tall at 14 px a tick, the great one 120
at 11. Pale; red with its black while the sword is drenched.

### The blood

**Enter**, held 3 seconds standing on the ground with nothing under way: the sword upright
before the face, the player held still, the red coming up the blade. Let go of, or off the
ground, and it is over with nothing. While the sword is held and could be drenched, Enter does
not open the chat.

| Blood Drenched Sword, 2 minutes | |
|---|---|
| the sword's damage | 1.75 times (1,050) |
| defense | 1.6 times |
| most life | three fifths |
| speed | 1.25 times: using any item, running, the jump |
| debuffs | every one the Nurse would take, taken as it begins |
| every blow of the sword | the enemy bleeding (Ultra Bleeding): 200 life a second, five seconds from the last blow |

When it ends, however it ends, **Dried Blood**: 2 minutes before it can be had again. It cannot
be clicked off, the Nurse cannot take it, and death does not.

Drenched, the sword is his bloodied one, burning black and red; its slashes are red; every blow
of it, of its red slashes and of its straight cuts leaves the enemy bleeding, blood spewing down
out of it; and the chain is **up, down, the smash and the leap**: at the nearest enemy in sight
within 40 tiles (10 tiles toward the cursor with none), a roll over and a cut down onto it, or,
with it over your head and you under it, straight up at it. The button still held as it ends:
down onto the point if in the air, then **the drive**: the sword into the ground, 70 ticks held
still, and twelve straight cuts out of the ground within fifteen tiles over its first 36 ticks,
every other one through an enemy there, each the drenched sword's whole damage, through
everything.

### A lore piece

Nothing of its own against what flies: its owner kept to the ground (by request, PADWorkFlow
§16.59). A Lore Item (§3), as the Blade of Mors is: always a favourite, taken back out of the
trash and out of a chest, one to a player, a place on the Lore Codex's shelf, no prefix ever
(one already on it goes at the next load), and worth nothing, so that no shop takes it. Its
verses, with Shift held, are in §3.

### Against the Zenith

Damage a second, a player with nothing on, the button held at a still enemy (PADWorkFlow §16.57
to §16.59 have how, and the sums), as measured on §16.59's build. The Zenith here is the mod's,
at 150 damage (below, The game's own items):

| | 40 by 56 | 160 by 160 | 40 by 56, 80 defense |
|---|---|---|---|
| the Zenith, 150 | 3,217 | 4,417 | 2,342 |
| the Zenith, the game's 190 (§16.58) | 4,074 | 5,593 | 3,192 |
| the chain | 1,342 | 1,342 | 1,332 |
| the air | 3,341 | 4,471 | the same |
| drenched, the chain with the leap and the drive, and the bleeding | 5,300 | 5,908 | the same |
| drenched, the air, and the bleeding | 7,340 | 9,474 | the same |

The chain works out at 1,337 whatever the size or defense (five cuts of 600 every 140 ticks, 4
in 100 crits). The bleeding adds its 200 a second to all that the drenched sword does. The
Zenith used under the blood (its speed only): 4,032 (5,106 at 190).

### Open

- Run in the game once (§16.58's picture: the dive that never found the ground, mended); the
  aim, the bleeding, the wider ring, the lore piece and its rarity not yet. Never heard, never
  tried with a second player.
- No source but Finish.
- Its picture is a copy of `SwordsmanBluntSide.png`; the two buff icons are stand-ins.
- A player struck in PvP is not bled, and the chain's aim looks for enemies only.
- The numbers that are mine, the readings to confirm and what was added without being asked:
  RELEASE_TODO.

---

## Hoard of Undead

A summoner's weapon before hardmode, by request (PADWorkFlow §16.63, §16.64): it summons a
zombie to fight for you, one minion slot each, 30 damage. It drops from any zombie, one time in
50; no recipe. Its picture and its buff's are stand-ins (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Summoner/HoardOfUndead.cs` + `.png` (40 by 40, a stand-in) | `HoardOfUndead`, `HoardOfUndeadDrop` | The item; the drop from every zombie |
| `Content/Projectiles/Summoner/HoardOfUndeadZombie.cs` | `HoardOfUndeadZombie` | The zombie: its AI and its moving, what it goes for, where it stands, its drawing |
| `Content/Buffs/HoardOfUndeadBuff.cs` + `.png` (32 by 32, a stand-in) | `HoardOfUndeadBuff` | Up while any are out; cancelled, they go |

### The item

Used as the game's own staffs are: 36 ticks with a swing, 10 mana, used again while held, the
summoning sound, a knockback of 2. Green, sells for 1 gold. The zombie comes up at the cursor,
or at your feet when the cursor is in the ground, and looks like one of the eight zombies of the
wiki's Zombie page at random (Zombie, Bald, Pincushion, Slimed, Swamp, Twiggy, Female, Torch):
the game's own pictures, drawn at run time.

### The zombie

| What | How |
|---|---|
| Moves | as the game's own zombie of its kind: `NPC.AI_003_Fighters` and the rest of `NPC.UpdateNPC` (gravity, tile collision, frames) taken over as 1.4.4 has them; with a zombie's numbers the same as the game's, tick for tick, in the rig; with yours (speed, liquids, gravity), as below |
| Speed | a zombie's 0.8 (Twiggy) to 1.2 (Swamp) and 0.65 more (yours): 1.45 to 1.85 a tick, 0.07 faster a tick to it; in liquids 1.65 times that, in shimmer 1.525 (yours), never into a block |
| Jumps | a zombie's 5, 6, 7 or 8 for what is in front of it, on land each 1.378 times as fast, so that against your gravity of 0.65 it gets up 6 tiles and not 7 (feet about 3, 4.2 and 5.5 tiles up against walls of 2, 3 and 4 or more, a step up the rest); 8 over a drop to a goal above; in Expert and Master 7.9 at a goal above it; `JumpHeight` and `Pull` set it; in liquids a zombie's own |
| Gives up | 60 ticks getting nowhere, then 540 ticks walking the other way |
| Goes for | the nearest enemy within 60 tiles of it (across plus up or down); the one you mark first |
| With none | back to you, to stand behind you (30 px, and 22 more for each zombie of yours before it) |
| Far | more than 75 tiles from you, put at your feet, from a fight too |
| Hits | 30 by touch, summon damage; the same enemy again after 35 ticks (yours; asked for 1.5 seconds, 90), another at once |
| Walls | blocks stop it; nothing takes it through one |
| Looks | the zombie's picture a fifth of the way to grey, at 70% of the light on it; an outline one picture pixel wide in #c3b8d1, #b2bfd9, #b6a5b8 and #89b2c4, going round it counter clockwise a turn every 2 seconds, at 70% (yours) |

### The drop

`HoardOfUndeadDrop`: one time in 50 from every NPC in `NPCID.Sets.Zombies` (27 kinds of the
game's; a mod can add its own) and from the Armed Torch Zombie, the three Zombie Elves, the
Blood Zombie, the Zombie Merman and the Maggot Zombie, with their small and big kinds: 50 in the
game's drop database. Not the Eyezor.

### Open

- Never run in the game, never heard, never tried with a second player.
- Both pictures are stand-ins.
- The numbers that are mine, the readings to confirm and what was added without being asked:
  RELEASE_TODO.

## Bone Squad

A summoner's staff after Plantera, by request (PADWorkFlow §16.69): each use calls a squad of
six of the Dungeon's skeletons, 5.5 minion slots in all, each with the game's own AI for it;
used again, the squad goes and six more come, picked anew. Every picture is the game's own,
asked of the game at run time; nothing of them is in the mod's folder. Its recipe is written out
and commented.

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Summoner/BoneSquad.cs` | `BoneSquad`, `BoneSquadPlayer`, `BoneSquadHeldGlow` | The staff; the squad's slots given back after a use; the outline round the staff in hand |
| `Content/Buffs/BoneSquadBuff.cs` | `BoneSquadBuff` | Up while any are out; cancelled, they go |
| `Content/Projectiles/Summoner/BoneSquadMinion.cs` | `BoneSquadMinion`, `BoneSquadBody` | What all six do (what they go for, the fetch, the damage, the drawing); the gravity and moving of those the ground holds |
| `Content/Projectiles/Summoner/BoneSquadWalker.cs` | `BoneSquadWalker`, `BoneSquadBoneLee`, `BoneSquadArmored`, `BoneSquadArcher` | `NPC.AI_003_Fighters`, and each walker's own part of it |
| `Content/Projectiles/Summoner/BoneSquadCaster.cs` | `BoneSquadCaster` | aiStyle 8: the Ragged Caster, the Necromancer or the Diabolist |
| `Content/Projectiles/Summoner/BoneSquadSkull.cs` | `BoneSquadSkull` | aiStyle 10: the Giant Cursed Skull |
| `Content/Projectiles/Summoner/BoneSquadShots.cs` | `BoneSquadShot`, `BoneSquadArrow`, `BoneSquadShadowBeam`, `BoneSquadInferno`, `BoneSquadInfernoBlast`, `BoneSquadLostSoul`, `BoneSquadShadowflames` | Their spells and arrows: the game's own, taken over |
| `Content/Projectiles/Summoner/BoneSquadDust.cs` | `BoneSquadDust` and its five kinds | The game's dust, drawn as the squad is |
| `Content/Projectiles/Summoner/BoneSquadLook.cs` | `BoneSquadLook` | How all of it is drawn |

### The item

Used as the game's own staffs are: 36 ticks with a swing, 10 mana, used again while held, the
summoning sound; 60 damage, the Pygmy Staff's knockback of 3. Yellow, sells for 10 gold. It
needs 5.5 slots (6, slots being whole); other minions go as the game sends them away for any
staff. The squad comes up at the cursor, or at your feet when the cursor is in the ground. Its
picture is the game's Skull, with the squad's outline round it in the inventory, on the ground
and in hand.

### The squad

| Who | Slots | Hurts by touch | Does |
|---|---|---|---|
| Bone Lee (287) | 1 | 90 | runs at 5 a tick, leaps at what it fights |
| Armored Bones, two different of the twelve (269 to 280) | 0.75 each | 40 to 85 by kind | walks at its kind's speed and 0.65 more (1.65 to 3.9) |
| Ragged Caster, Necromancer or Diabolist, one of six (281 to 286) | 1 | 35 to 60 by kind | stands, and casts twice as often as the game's; goes and comes back near what it goes for |
| Skeleton Archer (110) | 1 | 45 | stops and shoots flaming arrows |
| Giant Cursed Skull (289) | 1 | 60 | flies through everything, spits shadowflame |

| What | How |
|---|---|
| Moves | as the game's own NPC: `NPC.AI_003_Fighters`, aiStyles 8 and 10, and the rest of `NPC.UpdateNPC`, as 1.4.4 has them |
| Goes for | the nearest enemy within 60 tiles of it (across plus up or down); the one you mark first |
| With none | the walkers stand behind you (30 px, and 22 more for each walker before it); the caster comes to you; the skull keeps 40 behind and 60 over you |
| Far | more than 75 tiles from you, put at your feet (the skull at your middle), from a fight too |
| Brought back | Bone Lee and the Armored Bones, with nothing to fight, not back with you (within 10 tiles, nothing solid between) for 5 seconds together: put at your feet (§16.70) |
| Jumps | Bone Lee and the Armored Bones up to 10 blocks: their highest jump as high as the wall in front of them, or what they go for over a drop, needs, never lower than the game's 8 (§16.70); the archer the game's; none jumps while it stands at its place |
| Hits | its NPC's damage by touch, times the staff's damage over 60, summon damage; the same enemy again after 35 ticks |
| Spells and arrows | the game's own, 1.5 times as fast; the NPC's damage for them, the same way; each kind hits the same enemy again after 10 ticks; On Fire the only debuff |
| Looks | the NPC's picture a fifth of the way to grey, at 70% of its colour; an outline one picture pixel wide in #34eb4f, #6afc94, #16c90c and #a3de57 at 70%, going round counter clockwise a turn every 2 seconds; the arrow and their dust the same way |

### Open

- Never run in the game, never heard, never tried with a second player. The walkers' moving,
  their jumps and the melee ones' bringing back were run in a rig on the game's own engine
  (§16.70); nothing else of it.
- The numbers that are mine, the readings to confirm and what was added without being asked:
  RELEASE_TODO.

---

## Wooden Crossbow

A crossbow for the very start, by request (PADWorkFlow §16.73): slower and harder than the
game's Wooden Bow, its arrows faster. Its recipe is live since you uncommented it, and its damage
and knockback are yours (§16.75); its picture is a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Ranged/WoodenCrossbow.cs` + `.png` (52 by 32, a stand-in) | `WoodenCrossbow` | The item |

### The item

| What | How |
|---|---|
| Damage | 14 ranged; knockback 5 (yours, §16.75; 12 and 3, mine, before) |
| Use | 40 ticks a shot; not again by holding the button |
| Shoots | the arrows loaded, at 9 a tick (the Wooden Bow's 6.1) |
| Sound | the ballista tower's shot (`DD2_BallistaTowerShot`), higher and softer (mine) |
| Held | level, at the trigger: drawn from 6 pixels behind the point the game holds a gun at (`HoldoutOffset`), where the game draws one from 10 in front |
| Worth | White, sells for 2 silver (mine) |
| Recipe | 15 Any Wood and an Any Iron Bar at a Work Bench; live (you uncommented it, §16.75) |

### Open

- Never run in the game.
- The picture is a stand-in; the numbers that are mine: RELEASE_TODO.

---

## Mycellaneous

A bow for early on, by request (PADWorkFlow §16.73): every arrow it shoots that hits an enemy
sends its shooter back a little life, on a spore of blue light that flies home. Its recipe is
live since you uncommented it (§16.75); its picture is a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Ranged/Mycellaneous.cs` + `.png` (26 by 44, a stand-in) | `Mycellaneous`, `MycellaneousArrow` | The bow; its arrows, marked as it shoots them, which send the spore off a hit |
| `Content/Projectiles/Ranged/MycellaneousSpore.cs` | `MycellaneousSpore` | The spore: its flight home, the life landing, its drawing |
| `Content/Particles/SporeMote.cs` | `SporeMote` | The motes it sheds |

### The item

| What | How |
|---|---|
| Damage | 13 ranged; no knockback |
| Use | 20 ticks a shot; not again by holding the button |
| Shoots | the arrows loaded, at 7.5 a tick (the Wooden Bow's 6.1) |
| Life | 2 or 3 for a hit on an enemy, evenly; 5 for a critical hit. An enemy: not a critter, not friendly, not unhurtable, more than 5 life |
| Light | a little of the Glowing Mushroom's, about whoever holds it |
| Worth | Blue, sells for 20 silver (mine) |
| Recipe | 50 Glowing Mushrooms and a Wooden Bow, anywhere; live (you uncommented it, §16.75) |

### The spore

| What | How |
|---|---|
| Leaves | the struck enemy where the arrow hit, the way the arrow was going, up to 0.8 of a radian to either side, 3.5 to 5.5 a tick and 1.5 up |
| Out | 10 ticks, keeping 0.88 of its speed each tick |
| Home | to its shooter's middle: 3 a tick, 0.45 faster each tick, to 24; turning 0.08 of the way, 0.012 more each tick, to straight at them; never more than the distance left. 0.4 to 1.6 seconds after the hit |
| Lands | on touching its shooter: the life, with the green number (`Player.Heal`), on the shooter's machine only; none under Moon Bite |
| Else | its shooter dead or gone: nothing; further than 2,400 pixels, or 5 seconds flying: the life lands at once |
| Looks | a bloom (`BloomGeneric`) in deep blue, pale blue and white over a tail of light 8 ticks long; motes shed (`SporeMote`), one for each 8 pixels flown, up to 3 a tick; one of the game's glowing mushroom spores every 6 ticks; light; a burst where it leaves and where it lands; bigger for 3 and 5 life |
| Seen | by every player; on the others the landing shows when it reaches its shooter there or when the shooter's machine's kill arrives |

### Open

- Never run in the game, never tried with a second player.
- The picture is a stand-in; the readings and the numbers that are mine: RELEASE_TODO.

---

## Worminator

A summoner's weapon before hardmode, by request (PADWorkFlow §16.78): it summons a worm that
goes through the ground as the game's Giant Worm does, at twice its pace, put together as the
Stardust Dragon is: a head, three segments and a tail for the first summon, one segment more
for each summon after it. Every segment hurts for 8. It drops from the Giant Worm, one time in
5. Its pictures are the game's own, copied as they are for you to change; its buff's is a
stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Summoner/Worminator.cs` + `.png` (20 by 20, the game's Worm) | `Worminator`, `WorminatorDrop` | The item; the drop from the Giant Worm |
| `Content/Projectiles/Summoner/WorminatorSegments.cs` | `WorminatorSegment`, `WorminatorHead`, `WorminatorNeck`, `WorminatorBody`, `WorminatorTail` | The worm: its segments and their slots, the summon, the head's moving and the body's following, what it goes for, its drawing |
| `Content/Projectiles/Summoner/WorminatorHead.png`, `WorminatorBody.png`, `WorminatorTail.png` (18 by 30 each) | | The Giant Worm's own head, body and tail |
| `Content/Buffs/WorminatorBuff.cs` + `.png` (32 by 32, a stand-in) | `WorminatorBuff` | Up while the worm is out; cancelled, it goes |

### The item

Used as the game's own staffs are: 36 ticks with a swing, 10 mana, used again while held, the
summoning sound, a knockback of 2. Blue, sells for 1 gold. A new worm comes up out of the
ground under the cursor, going up (under you if there is no ground within 40 blocks under the
cursor); with a worm out, one segment more goes in before its tail.

### The worm

| What | How |
|---|---|
| Segments | the head, two segments that take no slot (drawn as bodies), a body, the tail: one slot; one body (one slot) more for each summon after the first. The head and the tail are never let go for another minion; a body is, and the worm closes up behind it. No body left: the worm goes |
| Moves | the Giant Worm's head (`NPC.AI_006_Worms`, type 10, as 1.4.4 has it): top speed 6, 0.05 a move to change it; in the ground it steers for what it goes after, out of it it falls (0.11 a move, to 6); each segment its width (14) behind the one ahead. Two moves a tick while it has an enemy or is more than 480 from you, one otherwise (yours: "2x speed") |
| Ground | a solid block not actuated, a platform of the first kind, a quarter tile of liquid (the game's) |
| Goes for | the enemy you mark, within 1,200 of you; else the nearest within 700 of you, kept while it stays within 700 |
| With none | circles under you, about 9 blocks under the ground below you, swinging 6 to each side, now and then breaking out |
| Far | more than 1,000 from you both ways, out of the ground: back through the air (the Giant Worm's rule); more than 2,400: put back in the ground under you |
| Hits | 8 a segment by touch, summon damage, each segment the same enemy again after 12 ticks; knocked away from the segment |
| Looks | drawn by the head, tail first, behind the blocks as the Giant Worm is (seen through them with a Hunter Potion); in the ground, its dirt: one time in 100 for each block a segment overlaps, each move; a spray where it breaks out and goes in |
| Sounds | its digging, the Giant Worm's at a quarter of its loudness, every 10 to 20 ticks in the ground; the Giant Worm's at full loudness when summoned |

### Open

- Never run in the game, never tried with a second player. Its steering was run beside a port
  of the game's own (§16.78).
- The pictures are the game's own, as asked; the buff's is a stand-in.
- The readings and the numbers that are mine: RELEASE_TODO.

---

## Vulture Staff

A summoner's weapon, by request (PADWorkFlow §16.78): vultures that sit on your shoulders in
two towers when there is nothing to fight, and circle and dive on the weakest enemy near you
when there is. One minion slot each. Nothing gives it yet: one source is written out and left
off. Its pictures are the game's Vulture's, copied as they are for you to change; its buff's is
a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Summoner/VultureStaff.cs` + `.png` (38 by 38, the Vulture perched) | `VultureStaff` | The item; the source, left off |
| `Content/Projectiles/Summoner/VultureMinion.cs` + `.png` (78 by 360, the game's Vulture) | `VultureMinion`, `VultureFrenzy` | The vulture: where it sits, what it does, its drawing; the frenzy |
| `Content/Buffs/VultureStaffBuff.cs` + `.png` (32 by 32, a stand-in) | `VultureStaffBuff` | Up while any are out; cancelled, they go |

### The item

12 summon damage, knockback 3; the rest as the Worminator's. Blue, sells for 1 gold. The vulture
comes out at the cursor and flies to its shoulder. Source left off: the Vulture, one time in 25.

### The vultures

| What | How |
|---|---|
| Sitting | on your shoulders, placed as the Baby Finch is placed on the head (turned with you, your head's bob as you walk): the first on the shoulder you face with, the second on the other, each next one on the back of the one two before it; 16 to each side of your head, 24 up and 3 further out a vulture up a tower; both towers behind you |
| Alive | they face your way, hop across when you turn, flap to keep their balance when you jump or land, stretch their wings now and then; the higher ones lean back as you run |
| Goes for | of the enemies within 640 of you and in your sight, the one with the least share of its life left (kept while it has no more than a twentieth more than that); the one you mark, within 1,000, first |
| Hunting | off the shoulders with a beat of wings, 18 ticks apart; one circle over it for all of them (220 by 77, 150 above it, a turn every 3 seconds); each dives after 48 ticks of circling, with a screech, 1.1 faster a tick to 15, for 40 ticks at most, a streak behind it; a hit or a miss: 18 ticks climbing, then the circle again |
| Hits | 12 by touch, summon damage, 25% more on an enemy under half its life; the same enemy again after 20 ticks; only while hunting |
| Frenzy | a kill by any of them: for 3 seconds they circle half as long between dives, the streaks red |
| With none | they fly home and sit |
| Far | more than 2,000 from you: back on the shoulder at once (the Baby Finch's) |
| Looks | the game's Vulture: frame 0 sitting, 1 to 5 flying (5 ticks each), 3 diving, the beak along the dive |

### Open

- Never run in the game, never tried with a second player. Its hunting was run in a flat
  scene (§16.78).
- No source yet. The pictures are the game's own, as asked; the buff's is a stand-in; the
  perch is set for the game's Vulture, whose size is large on a player.
- All its numbers are mine: RELEASE_TODO.

## The game's own items

`Content/Items/VanillaBalance.cs`: the game's items with this mod's numbers, put on after the
game's own (PSItem stays as it is).

| item | the game's | here |
|---|---|---|
| Zenith | 190 damage | 150 damage, nothing else of it changed (PADWorkFlow §16.59) |

Recipes for the game's items are in `PADRecipes.cs`, live, as the Greater Mana Potion's is (the
mod's own items' recipes were kept commented out; since §16.75 some are live: yours, uncommented,
and the Trumpet's). Five were added by request (PADWorkFlow §16.71); the game has a recipe for
none of them:

| item | from | where |
|---|---|---|
| Cloud in a Bottle | 25 Clouds, a Bottle | anywhere |
| Whoopie Cushion | 3 Poo, 2 Silk | anywhere (no place was named; mine) |
| Ice Skates | a pair of Boots, 4 Iron Bars (any), 20 Ice Blocks | a Tinkerer's Workshop |
| Hermes Boots | a pair of Boots, 50 Clouds | a Tinkerer's Workshop |
| Water Walking Boots | a pair of Boots, 50 Gel, a Water Bucket (it goes, bucket and all) | a Tinkerer's Workshop |

`Content/NPCs/VanillaLoot.cs`: the game's items in the game's enemies' loot.

| enemy | drops | chance |
|---|---|---|
| Voodoo Demon | Molten Charm | 1 in 10, every mode, rolled beside the Guide Voodoo Doll it always drops (§16.71) |

## The mod's own rarities

`Content/Rarities/BlasphemousCrimson.cs` (PADWorkFlow §16.61, §16.62). To the game a rarity is
one colour; a name in these is drawn by hand in the tooltip (`RampedNames`, through
`MagicColours.Ramped`), each row of each letter in its own colour, and shows the one colour
wherever the game can show only one (the name that rises from an item picked up). The colours
move: the letters' down them and the outline's up it, each coming round from its last back into
its first, a turn every `FlowSeconds`; at any moment two thirds of a turn spans the name.

| rarity | the letters, moving down | their outline, moving up | a turn | the one colour | items |
|---|---|---|---|---|---|
| Blasphemous Crimson | #641e07, #431938, #7b0a31 | #c42727, #d13d62, #c40808 | 2 s | #d13d62 | Foigh's Rust Side Sword, for now the only one (yours) |

---

# 2 · Consumables

`Content/Items/Ranged` for ammunition; elsewhere by type.

| item | kind | state |
|---|---|---|
| **Great Arrow** | ammunition | to write |

## Great Arrow

The Greatbow takes nothing else, and nothing else takes Great Arrows.

- arrow: `Item.ammo = ModContent.ItemType<GreatArrow>()`, `Item.shoot` = the lodging
  projectile.
- bow: `Item.useAmmo = ModContent.ItemType<GreatArrow>()`.

One per draw, **ten to fifteen per rain**. They are the weapon's economy and the reason
the special attack is a decision rather than a rotation.

**[OPEN]** Source. Craftable, bought, or finite — and finite is the interesting one, given
what the weapon is.

---

# 3 · Lore items

`Content/Items/LoreItem`. `Item.PAD().Lore = true`, verses in `Item.PAD().LoreText`, `\n`
between them, shown on Shift. Voice rules in the `pad-lore-voice` skill: two to four
verses, ≤ 60 characters each, verse 1 what it is, verse 2 who held it, verse 3 the gap.

| item | state |
|---|---|
| Melee Decider | built |
| Pouch of Berries | built, sprite redraw outstanding |
| Lore Lock (system) | built |
| Game Changing Orb | built |
| Foigh's Rust Side Sword | a weapon (§1); a lore item since §16.59 |

## Greatbow of a Pilgrim — text

The bow is a weapon, not a lore item, but it carries lore text. All proposals.

**Name.** *Greatbow of a Pilgrim.* Indefinite article. He is not named on it.

**Flavour** (≤ 55 chars):

> `'The string is hair. He did not say whose.'`

**Verses** (≤ 60 chars each):

> Rebuilt from a break, and it does not draw as it did.
> The man who carried it spent it on one animal.
> The wood is not from here.
> Nobody asked him and he is still walking.

## Foigh's Rust Side Sword — text

Yours since §16.62 (written in by you on the 11th; PADWorkFlow §16.62 has them). **Flavour**,
the tooltip's last line:

> `'The rusted side was used on one man, for centuries.'`

**Verses**:

> Its jagged rusted side was used for a three hundred year long pursuit of vengeance
> The pristine side awaits a worthy foe
> 'The muscle hardens to rust, the sword bleeds with the body'

The tooltip's other lines: "Ignores all defense", "Right click shoots up to three slashes.",
"Hold Enter for 3 seconds, standing, to drench it in blood". Mine before them are gone.

---

# 4 · Magic — the memory system

**[BUILT. §4.1–4.6 are the design as argued, kept for the reasoning; §4.7 is what exists,
and wins where they differ.]**

Lore for all of it is §XXIV of the Bible, *Where magic comes from* — drawn out of the
land, not given by gods.

## 4.1 The shape

Spells and incantations are **not held items**. They are memorised, and cast through a
catalyst held in the hand.

| | |
|---|---|
| **Memory** | one shared pool. Starts at 2, raised by a consumable, hard cap **10**. |
| **What fills it** | spells AND incantations, from the same pool. |
| **Catalysts** | a **staff** casts spells. A **seal** casts incantations. |
| **Casting** | left click, with the catalyst in hand. |
| **Selecting** | two keybinds, up and down. |

**Cycling skips what the held catalyst cannot cast.** Holding a staff, up and down move
between the memorised *spells* only; the incantations stay memorised and keep their slots
but are not in this cycle. This is the whole reason there is no "selected an incantation
while holding a staff" case — it cannot arise, and a cast that does nothing is the worst
feedback a fight can give. The selection is remembered **per catalyst**, so staff → seal →
staff returns to the spell that was up.

**The selection UI only appears while a catalyst is held.** A full memory screen lives at
the table below.

## 4.2 Why one pool, and why it must cost something to change

One pool is what makes a build. Two pools of ten would mean everyone fills both and
nothing is chosen; one pool of ten means six spells and four incantations is a decision,
and ten incantations is a different character.

**Memorising happens at a placed tile, not anywhere.** If the loadout can be rearranged
mid-cave for whatever is in the room, the cap constrains nothing and the system is an
inventory with extra steps. The friction is the design.

**Ten slots wants twenty to thirty castables** before the cap bites. Below about twice the
cap, everyone memorises nearly everything and the choice is decoration. Content
commitment, noted here so it is a decision rather than a surprise.

## 4.3 Spell or incantation

Same source — there is no divine half, see §XXIV — so the split is **how it is drawn**,
not where from.

- A **staff** is an instrument. The Arcanum's engineering: focused, projected, a thing
  that leaves the caster and travels.
- A **seal** asks the ground directly. Slower, wider, comes out of the world rather than
  out of the hand. Terrain, zones, growths.

Both drink the same gas.

## 4.4 Cost

Everything costs mana. Some cost something else on top, and **life is the mod's existing
vocabulary for that** — the Handswitch already trades lifeforce for powerful shots.

**On vanilla mana.** The wish to make regeneration less generous is fair, but a global
change rewrites every vanilla magic weapon for anyone with this mod installed, and someone
playing Demon Scythe in a PAD world did not ask for that. Mana Sickness already exists as
the anti-spam lever, and Calamity notably does not touch regen either — it adds expensive
weapons and its own resources instead.

Preferred order:

1. Make PAD's own casts expensive. A forty-mana spell self-limits and touches nothing.
2. Secondary costs, as above.
3. If the global change is still wanted, it is **a config toggle, default off** — a
   difficulty option rather than an imposition.

## 4.5 Type and element

Two labels on every magic weapon, spell and incantation, in the tooltip.

- **Type** is what it does: explosion, heal, pierce, and so on.
- **Element** is where it was drawn from: blood, frost, flame, wind, lightning,
  chlorophyte, decay, hallow, and whatever else the world has enough of.

They are **labels, not a grid**. Nothing requires every type-element pair to exist.

**Elements are regions**, because the power comes out of the land (§XXIV). The list looks
unrelated because biomes are unrelated, and that is correct rather than a problem. Draw
elements from what the mod has actually built — blood from the Abomanation, chlorophyte
from the rifle and `ChlorophyteSpill`, lightning from `Lightning` and Rage of a Thunder —
so the taxonomy reads as this world's physics instead of a genre checklist.

**Ship it as tooltip text first.** As labels it costs nothing and teaches a vocabulary.
The moment it is mechanical — resistances, weaknesses — every enemy in vanilla needs
resistance data, which is an enormous table and a permanent maintenance burden. Design the
labels so they *can* become mechanical later; that addition is additive, and the reverse
is a rewrite.

**[OPEN]** Whether an element cast on the ground it came from behaves differently, and
whether an element can only be learned in its own biome.

## 4.6 What has to be built, in order

1. `ModPlayer` memory: the list, the cap, save, load, and sync.
2. The two catalyst base classes, staff and seal.
3. The two keybinds and the per-catalyst remembered selection.
4. The cycle HUD, shown only while a catalyst is held. `MagazineHUD` is the nearest
   existing shape to copy.
5. The memory tile and its full UI.
6. The memory consumable, capped at 10.
7. Then, and only then, spells.

## 4.7 What is built

**This section is the state of the code. Where it and §4.1–4.6 disagree, this wins.**

### The machine

| Part | File | What it does |
|---|---|---|
| Magic Arsenal | `Content/PADplayer/MagicArsenal.cs` | `ModPlayer`. One pool of `MaxSlots` 10, `StartSlots` 3 open. `Unlocked` (earned), the slots (air included, so a cast keeps its place) and the selection per kind are saved. No two of the same cast. Slots take or give only within `HomeReach` (800 px, 50 tiles) of a town NPC's home tile. |
| Arsenal window | `Content/UI/MagicArsenalUI.cs` | Ten slots beside the inventory, shut ones banded. Shares a corner with the Lore Codex; opening one closes the other. Right click a cast to select it; the selected one per kind wears the ember frame. Away from a house the slots show dimmed and a click says why. |
| `Castable` | `Content/Items/Mage/Castable.cs` | Abstract `ModItem` for every spell and incantation. `Kind`, `CastDamage`, `Mana`, `UseTime`, `Shot`, `CastType`, `Element`; virtual `Channel`, `Speed`, `Knockback`, `ReuseDelay`, `CanCast`, `Cast`. Does nothing in the hand. |
| `Catalyst` | `Content/Items/Mage/Catalyst.cs` | Abstract staff or seal. No numbers of its own: every tick it copies damage, mana, use time, reuse delay, channel and shot from the selected cast of its kind, so vanilla's mana, timing, tooltip and prefixes work on it. Nothing selected, `CanUseItem` is false. |
| Keybinds | `PADKeybinds.SpellUp` (Up), `SpellDown` (Down) | Only while a catalyst is held, and only through casts of its kind. |
| Colours | `Content/Items/Mage/MagicColours.cs` | Fill and stroke per kind and per element; see below. |
| Makeshift Altar | `Content/Tiles/MakeshiftAltar.cs`, item in `Content/Items/Misc/` | Crafting station, 3×2, where casts are written. Nothing else. |
| `/magicarsenal` | `MagicArsenalCommand.cs` | **Test only** (RELEASE_TODO). Bare: reports open and earned. `max`: all ten open (`Maxed`, saved). `normal`: back to `Unlocked`; a cast in a slot that shuts moves to the first empty open slot, or is given back to the player. `Unlocked` is never touched, so going back is exact. |

**Where it left the design:**

- Starts at **3** slots, not 2.
- Memorising happens **near a lived-in house**, not at a memory tile. The Makeshift Altar
  is only where casts are crafted.
- **Nothing raises `Unlocked` yet.** The slot consumable (§4.6 step 6) is not built; only
  `/magicarsenal max` opens more.
- The selection is remembered per kind (spell, incantation), which with two catalyst kinds
  is the same as per catalyst.

### Catalysts

| Catalyst | Class | Kind | Source |
|---|---|---|---|
| Branch Staff | `Staves/TreeBranchStaff` | Spell | Dryad's shop |
| Clothier's Chatelaine | `Seals/SealOfAClothier` | Incantation | Clothier's shop; Skeletron (classic drop, bag in expert and master) |

The only seal comes after Skeletron, so no incantation can be cast before him.

### Casts

| Cast | Class | Kind | Type | Element | Damage | Mana | Use time | Rarity | Source |
|---|---|---|---|---|---|---|---|---|---|
| Cold Call | `Spell/ColdCall` | Spell | Evocation | Frost | 36 | 14 | 25, then 15 reuse delay | Green | Altar: 30 Ice Block, Book, 5 Sapphire |
| Aquatic Firearms | `Spell/AquaticFirearms` | Spell | Conjuration | Conquest | 92 (the S.D.M.G.'s shot) | 50, then 30/s after 3 s | 20, channel | Red | **none yet** |
| Meagre Flame | `Incantation/MeagreFlame` | Incantation | Evocation | Flame | 40 | 18 | 60, channel | Green | Fire Imp, 1 in 20 |
| Court of Hands | `Incantation/CourtOfHands` | Incantation | Conjuration | Blood | 30 | 18 | 45, channel | Green | Altar: 20 Essence of the Fouled, Book |
| Everhunger | `Incantation/EverHunger` | Incantation | Conjuration | Blood | 51 | 18 | 30 | Green | Altar: 29 Essence of the Fouled, Book |
| Forest, Nurture Me | `Incantation/ForestNurtureMe` | Incantation | Abjuration | Flora and Fauna | 0 | 25 | 30 | Blue | Dryad's shop |
| Chlorophyte, Arm Me | `Incantation/ChlorophyteArmMe` | Incantation | Transmutation | Flora and Fauna | 0 | 30 | 30 | Lime | Witch Doctor's shop, after Plantera |
| Rage! Sweet Fire | `Incantation/RageSweetFire` | Incantation | Enchantment | Flame | 0 | 40 | 30 | Pink | Altar: Lava Bucket, 15 Soul of Fright, 50 Living Fire Block, Book |
| Betsy's Flames | `Incantation/BetsysFlames` | Incantation | Transmutation | Draconic | 84, to each thing in the breath every 5 ticks | 30, then 30/s after 1 s | 20 | Yellow | Betsy, 1 in 20 (classic); her bag, 1 in 20 |

All under `Content/Items/Mage/`. Nine casts against ten slots: §4.2 wants twenty to thirty.

### Element colours (`MagicColours`)

| Element | Fill | Stroke |
|---|---|---|
| Flame | `#792620` | `#d77f2d` |
| Blood | `#ff4242` | `#680303` |
| Frost | `#9fe2ff` | `#1d3b7a` |
| Flora and Fauna | `#0f3311` | `#1d9f49` |
| Conquest | `#7a3a24` | ramp, bottom to top: `#bdab51` → `#c8a55b` → `#dbd88f` |
| Draconic | `#b0a158` | `#db184c` |

Kind words: Spell `#38462b` on `#da7210`; Incantation `#d65a51` on `#5d7ba2`.

**A ramp stroke** (`StrokeRamps`, `RampOf`) cannot come from vanilla's shadow pass, which
draws a glyph in one colour. `Stroked(..., ramp)` lays the text out word by word exactly as
`ChatManager` does (the same kerning and spacing through `DynamicSpriteFont`'s public
`SpriteCharacters`), draws the four outline offsets at spread 2 a glyph row at a time, each
row in its colour along the ramp, then the fill over it with `ChatManager.WaveColor`. Plain
text only, no colour codes. `Castable` and `Catalyst` pass the ramp to the element and
"Up:" lines. Add an element with a ramp by adding one entry to each dictionary.

### Aquatic Firearms

Held, a Minishark, Megashark and S.D.M.G. hang over the caster, strongest on top (offsets
0,-38 / 0,-63 / 0,-92), turn to the cursor and fire musket balls.

- **Numbers are read off the real guns** (`AquaticFirearmsBattery.Stats`, from
  `new Item(type)`, so PSItem's use times 6 / 6 / 4 are in): use time, speed, knockback,
  crit, sound, and vanilla's scatter (±0.4 per axis, ±0.2 for the S.D.M.G.).
- **Shot damage is gun + musket ball, rounded:** 13 / 32 / 92 before magic bonuses.
  *(Designed as ×2/3; changed to full by the user. The doc comments in
  `AquaticFirearmsBattery.cs` still say ×2/3.)*
- No ammo. The bullets are `ProjectileID.Bullet` from `GetSource_FromThis()`, set to
  magic damage and given the gun's crit after spawning.
- **Mana:** the 50 cast pays for `FreeTicks` 180; then `DrainPerSecond` 30, paid a point at
  a time. Let go, run dry, put the staff away or change spell, and they leave.
- **Look:** `AquaticFirearmsSkin` paints each gun's silhouette with a deep-sea nebula and
  stars drifting in two layers (`AquaticNebula.png` 256×64, `AquaticStars.png` 128×64),
  CPU-painted, no shader. Rim and halo are tinted by a glow that swings between two colours
  per gun (`GlowA` / `GlowB` in the battery). In: three Bezier ribbon waves out of the caster's
  back, each gun growing out of its wave (ease-out-back). Out: the same backwards, top
  gun first (ease-in-back). The wave sides are frozen at cast, so turning does not flip
  them.
- One-off sounds and bursts go through a `played` bitmask with a 6-tick late window, so a
  skipped or repeated tick neither loses nor doubles them.
- One battery at a time (`CanCast`: `ownedProjectileCounts == 0`). The tooltip's damage
  line shows all three shots.

### Chlorophyte, Arm Me

Puts on **Armed** (`Content/Buffs/Armed.cs`) for 20 s; will not recast while more than
5 s are left. `ArmMeCall` is the cast effect only (green motes drawn in, a bloom,
chlorophyte dust and jungle leaves).

- **The turning** is `ArmedRounds`, a hook on
  `On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float`
  (every overload ends there). A lesser bullet whose owner is Armed is made a
  `ChlorophyteBullet` instead, with its damage, speed and knockback as given: it homes and
  hits as hard as it would have, and loses its own effect (a Cursed Bullet's fire).
- **Only the player's own shots:** the source must be an `EntitySource_Parent` whose entity
  is the owner, or a non-hostile projectile of theirs. NPC bullets never turn. Covers
  Aquatic Firearms' guns.
- **Lesser** is worked out in `PostSetupContent`: a bullet projectile takes the lowest
  rarity among the ammo that **defines** it (vanilla ammo for a vanilla bullet, the same
  mod's ammo for a modded one) and is lesser below Lime. Luminite and Chlorophyte Bullets
  stay. The list is logged to `client.log` at load. Vanilla result: 12 kinds (14, 36, 89,
  104, 242, 279, 283, 284, 285, 286, 287, 981).
- **Why "defines":** LuiAFK Reborn's Unlimited ammo clones every vanilla bullet at rarity
  10. Letting all ammo vote made every kind rarer than chlorophyte and nothing turned.

### Rage! Sweet Fire

Puts on **Sweet Fire** (`Content/Buffs/SweetFire.cs`) for 30 s; will not recast while more than
5 s are left. While it lasts, the caster's fire hits deal 25% more (`SourceDamage += 0.25`, in
`SweetFirePlayer.ModifyHitNPCWithProj` and `ModifyHitNPCWithItem`). `SweetFireCall` is the cast
effect only (embers drawn in, then a burst of flame).

- **What is fire** is `FireAffinity` (`Content/Combat/FireAffinity.cs`), read out of the 1.4.4
  IL: everything vanilla lets set On Fire!, Hellfire or Daybreak (`Projectile.StatusNPC` by
  projectile type, `Player.StatusToNPC` by item type for a swing), the Magma Stone's and the
  Flask of Fire's melee exactly as vanilla gives them, the rest of the solar weapons and tools,
  the Hellfire Arrow, Meagre Flame's ball and Betsy's Flames' breath. Both flamethrowers, the
  Elf Melter's cold flame included. **Not** Cursed Inferno or Shadowflame (the user: dark), nor
  Frostburn. Never an NPC's or a trap's projectile.
- **Other mods** are learned as they are used (`FireAffinityPlayer`): the first time one of
  their projectiles, or a swing of one of their items, sets what the player hits on fire, that
  type counts from then on, for the session (the first hit itself is not raised). "On fire" is
  On Fire!, Hellfire, Daybreak, or any other mod's debuff whose internal name says fire
  (`FireAffinity.NamedFire`: a word starting Fire, Flame, Burn, Blaz, Inferno, Ember, Scorch,
  Ignit, Magma, Lava, Pyro, Incinerat, or ending fire, flame(s), burn, blaze; none starting
  frost, ice, cold, chill, freez, frozen, cryo, snow, glacial, curse, shadow, dark, void),
  found at load and listed in `client.log`. Not learned while a Magma Stone or Flask of Fire
  could be the burn, and never a vanilla type. A mod can also say so:
  `Mod.Call("FireProjectile" | "FireItem" | "FireDebuff", type)`.
- **The ring** (`Content/Particles/SweetFireAura.cs`, one per player on every client): three
  flames going round at the waist on a flattened circle, 30 by 10, once in about 1.5 s, each
  with a 12-dot tail that cools white to yellow to orange to red and rises 10 px, the far side
  dimmer; embers now and then; light by design. A premultiplied soft dot in the alpha pass
  (adds its light, covers a little), so it keeps its colour on a bright sky.

### Betsy's Flames

Turns the caster's head into Betsy's and breathes her fire at the cursor for 5 seconds
(PADWorkFlow §16.73, §16.74). `BetsysFlamesBreath` is the whole of it, from the change to the
head going back; `BetsysFlamesPlayer` hides the caster's own head and `BetsysFlamesHead` draws
hers.

- **The order:** the change (48 ticks: their head bursts, and hers heaves up out of the neck in
  seven throbs, raw and dead-eyed until her eyes open at 39), the roar (30: her scream, two
  rings of fire, the head thrown back with the jaw wide, the screen shaking, the only time it
  does), the breath (300), then the last flames leave (60): the mouth shuts over the first 16
  of them and the head sags and shrinks back over the last 16.
- **The breath is the game's** (type 687's drawing, collision, light, dust, smoke and fire),
  held for the 5 seconds: the game's is one burst of 78 ticks. Half as far again as hers
  (`Reach` 600) at her flames' speed (a puff 60 ticks out, 22 of them), and through walls as
  hers. It hurts each thing in it every 5 ticks (`HitCooldown`) for 84: 1,008 a second,
  measured against the Nebula Arcanum.
- **Her head** is the user's picture (the game's `Gore_1081` facing left, the neck off, the eyes
  opened): the game's pixels drawn from the gore at run time in strips 2 pixels tall
  (`BetsysFlamesHeadCut`), the user's own 260 from `BetsysFlamesHead.png`; half size, the back
  of the jaw on the neck; the game's dead face until the eyes open. The jaw turns about the
  mouth's corner (`JawMost` 0.5 at the roar, 0.36 breathing) over the inside of the mouth
  (`BetsysFlamesMaw.png`) and its fire (`BetsysFlamesMawGlow.png`, drawn added); the head is
  turned so that the breath, leaving the middle of the mouth (`BreathTilt` 0.18 under the way
  it faces), goes along the aim.
- **Mana:** the cast's 30 pays the first second; then `ManaPerSecond` 30, a point at a time. No
  mana, no seal, another cast picked or the caster held, and it stops; the head goes back either
  way. Its tooltip's mana line says a second.
- **Aim** turns at most `TurnRate` 0.12 a tick; the owner sends it on when it has moved 0.03, at
  most every 3 ticks.
- One-off sounds and bursts go through a `played` bitmask with a 6-tick window, as Aquatic
  Firearms' do. `FireAffinity` counts the breath as fire.

---

# 5 · Accessories

`Content/Items/Accessories`

## Triple Fart In A Horse Shoe Adorned Bottle

By request (PADWorkFlow §16.66): *"as the name suggests: it creates allow quadruple jumps and
negates fall damage, you know what the recipe is."* Its picture is a stand-in
(PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/TripleFartInAHorseShoeAdornedBottle.cs` + `.png` (28 by 32, a stand-in) | `TripleFartInAHorseShoeAdornedBottle`, `TripleFartJump`, `TripleFartPlayer` | The item and its recipe; its jump; how many of its jumps are left, and no fall damage |

### What it does

| What | How |
|---|---|
| Jumps | three in the air, one after another, each the Fart in a Jar's own: held up twice as long as a jump off the ground (30 ticks to 15), 1.75 times as fast across while it lasts, its sound and its green cloud; with the jump off the ground, four (the game calls the Bundle of Balloons' four "quadruple") |
| Their order | tried right after the Fart in a Jar's, before the Tsunami's and the Cloud's: worn with a Cloud in a Bottle, its three and then the cloud |
| Back | all three once on the ground again, as every jump in the air comes back |
| Falls | no fall damage, with wings or without (`noFallDmg`, as a Lucky Horseshoe gives it; given again after the mod's grounding of wings takes it from one wearing wings) |
| Worth | Yellow, 20 gold to buy and 4 to sell: the Bundle of Horseshoe Balloons' |
| Tooltip | the game's own words for what it does, and a line of mine: *'The horseshoe is for luck. So is the cork.'* |
| Recipe | three Fart in a Jar and a Lucky Horseshoe at a Tinkerer's Workshop (my reading of "you know what the recipe is"); commented out, as every recipe is |

### Open

- Never worn in the game.
- The picture is a stand-in; the recipe is commented out.
- What was mine: RELEASE_TODO.

---

## Boots

By request (PADWorkFlow §16.71): *"Boots equipable, doesnt have any affect but wear on shoes
(reuse wearing texture of hermes boots)"*, made from 10 Silk, 5 Leather and 2 Iron Bars. What
three of the game's boots are now made from. Its picture is a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/Boots.cs` + `.png` (26 by 24, a stand-in) | `Boots` | The item; its recipe |

### What it does

| What | How |
|---|---|
| Worn | nothing at all |
| On the feet | the Hermes Boots' own picture (`Item.shoeSlot` is theirs, `ArmorIDs.Shoe.HermesBoots`); the slot's eye hides them as any shoe |
| For | a pair goes into the Ice Skates, the Hermes Boots and the Water Walking Boots, at a Tinkerer's Workshop (`PADRecipes`) |
| Worth | White, sells for 20 silver: a fifth of the Hermes Boots' worth (mine) |
| Tooltip | *'Plain enough. Someone handy could make more of them.'* |
| Recipe | 10 Silk, 5 Leather, 2 Iron Bars (any), anywhere: no place was named (mine); live since you uncommented it (§16.75) |

### Open

- Never worn in the game.
- Its recipe is live (§16.75), and with it the three boots made from them.
- The picture is a stand-in. What was mine: RELEASE_TODO.

---

## Metallurgist's Brilliant Stone

By request (PADWorkFlow §16.71): *"In a 38(wide)x39 rectangle, every ore that can be turned on
by spelunker, will be draw with no light level. Sold by Merchant for 30 gold."* The field made
58 by 45 by request (§16.72). Its picture is a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/MetallurgistsBrilliantStone.cs` + `.png` (24 by 24, a stand-in) | `MetallurgistsBrilliantStone`, `MetallurgistsBrilliantStoneShop`, `MetallurgistsStonePlayer`, `MetallurgistsSight` | The item; the Merchant's shop; who wears it, and the glints; the drawing and its IL edit |

### What it does

| What | How |
|---|---|
| Where | 58 by 45 tiles about the tile its wearer's middle is in: 29 to the left, 28 to the right, 22 up, 22 down (§16.72; 38 by 39 before); the wearer alone sees it |
| Which | the game's ores the Spelunker Potion lights (`TileID.Sets.Ore` and `Main.IsTileSpelunkable`): copper, tin, iron, lead, silver, tungsten, gold, platinum, meteorite, cobalt, palladium, mythril, orichalcum, adamantite, titanium, chlorophyte. Not demonite, crimtane or hellstone (the potion does not light them); not the gems |
| How it looks | as in full light, however dark it is there; slopes and half blocks as the game draws them; painted ore keeps its paint; under the Echo Coating only where the game shows it; ore turned off by an actuator not drawn; now and then the potion's own spark, for 1 ore in 300 a tick (mine) |
| How | drawn a second time over the game's own, through the game's `TileDrawing` (`GetTileDrawData`, the private `DrawBasicTile`), once a frame after `Main.DoDraw_Tiles_Solid` ends its batch and before it calls `Main.DrawTileEntities` (an IL edit); what stands among the tiles, the cracks of mining, NPCs, players and projectiles are drawn over it |
| Worth | Green (mine); the Merchant asks 30 gold, moved by his mood as all his prices are |
| Tooltip | "Shows the ore near you as if in full light"; *'The metal answers it, even in the dark.'* |

### Open

- Never worn in the game.
- A whole block of ore beside a half block is drawn whole, where the game draws it in two parts.
- The picture is a stand-in. What counts as ore, and what was mine: RELEASE_TODO.

---

## Dryad's Blessing

A four leaf clover, by request (PADWorkFlow §16.71): *"Will survive an attack that will kill you
with 25 HP, Gives you a buff that boosts your regen by 20hp/s for 5 sec and 1.5 sec of
invinciblity"*, a sound and leaves circling, ready again in 15 minutes (10 since §16.72) or at a
respawn. The Dryad gives it, for a Bezoar (§16.75). Its pictures are stand-ins
(PlaceholderTextures.md); its sound was made here.

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/DryadsBlessing.cs` + `.png` (26 by 28), `DryadsBlessingLeaf.png` (12 by 12) | `DryadsBlessing`, `DryadsBlessingPlayer`, `DryadsBlessingLeaves`, `DryadsBlessingLeavesBack`, `DryadsBlessingLeavesFront` | The item; sparing its wearer, the sound and the burst; the ring of leaves, behind and in front of the wearer |
| `Content/Buffs/Spared.cs` + `Spared.png`, `WiltedClover.png` | `Spared`, `WiltedClover` | The regeneration after; the wait |
| `Content/Sounds/DryadsBlessing.ogg` | | Its sound, made here (2.3 s, mono, Vorbis at quality 8) |

### What it does

| What | How |
|---|---|
| Spares | a hit through the game's `Player.Hurt` that would take the last of its wearer's life leaves them at 25 (or all their life, if they have less at most): any hit, a fall, lava and spikes included. Not burning, poison and the other harm a second, drowning, or the deaths the story deals out by name |
| Then | a second and a half in which no hit lands (`SetImmuneTimeForAllTypes(90)`); Spared, 5 seconds of 20 life a second more (`lifeRegen` 40, after what burning and the like take) |
| Again | after Wilted Clover, 10 minutes (§16.72; 15 before), or at a respawn (death takes it); saved with the player, the Nurse does not cure it, expert does not lengthen it |
| Seen and heard | its sound; green motes (the Dryad's Ward dust), grass and sparks; ten leaves on a tilted ring about the wearer's middle while Spared lasts, six of the game's forest leaf and four of the clover's, bursting out in a third of a second, going round about once in a second and a half, drifting off and fading in the last three quarters; a soft green light |
| How | `OnHurt` notes the hit that takes the last life; `PreKill`, on the wearer's machine, spares them; the others are told their life by the game's `PlayerLifeMana` and draw the leaves from the buff |
| Worth | Orange, sells for 2 gold (mine) |
| Tooltip | what it does, three lines; *'She does not let go of what she is handed.'* |
| Source | the Dryad, by request (§16.75): one chat in four she speaks of a wound a Bezoar would draw out, until the player has brought her one; with a Bezoar on them, "The Bezoar" under her dialogue takes it, and she thanks them and gives them the clover; once for each player (`DryadBezoar.cs`) |

### Open

- Never worn in the game; its sound measured, never heard.
- The Dryad's wound and her Bezoar never run in the game (§16.75).
- The pictures are stand-ins. What it spares, and what was mine: RELEASE_TODO.

---

## Necklace Of The Academy

By request (PADWorkFlow §16.71): *"(first hint at the Havu Lainey Magic Academy) has a bool
BasicNecklace in globalplayer, +1 slot in arsenal, +5% magic damage. Obtained 1/15 chance from
dark caster."* Its picture is a stand-in (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/NecklaceOfTheAcademy.cs` + `.png` (24 by 32, a stand-in) | `NecklaceOfTheAcademy`, `NecklaceOfTheAcademySource` | The item; its drop |
| `Content/PADplayer/PADPlayer.cs` | `PADPlayer.BasicNecklace` | Set every tick it is worn |
| `Content/PADplayer/MagicArsenal.cs` | `MagicArsenal` (`Necklace`, `OpenSlots`, `NecklaceSlot`, `HoldsOpen`) | The slot it holds open |
| `Content/UI/MagicArsenalUI.cs`, `MagicArsenalCommand.cs` | | The slot drawn, hovered and clicked; `/magicarsenal` says so |

### What it does

| What | How |
|---|---|
| Worn | `PADPlayer.BasicNecklace`; one more Magic Arsenal slot, the first past those earned (none more with all ten earned); 5% more magic damage |
| Its slot | a little bluer than the rest, with a small picture of the necklace in its corner; hovered empty, says it is held open by the necklace (mine) |
| Taken off | the slot shuts with whatever is in it: a cast there is kept, not cast, not up, not in the cycle, shown faint over the bands; a click near a house gives it back; nothing goes in |
| Drop | the Dark Caster, 1 in 15, every mode |
| Worth | Green, sells for 1 gold (mine) |
| Tooltip | "One more Magic Arsenal slot", "5% increased magic damage"; *'A first year's necklace. The clasp reads H. L.'*, the academy's first hint (the Lore Bible names no academy yet) |

### Open

- Never worn in the game.
- The picture is a stand-in, and its mark in the slot is small (12 px). What was mine:
  RELEASE_TODO.

---

## Bone Casing

By request (PADWorkFlow §16.71): *"Gives a ribcage shaped shield over the entire body, wont
disappear until you die. ribcage has 100HP, every attack on the player will be absorbed by
this"*, as the Dark Prince's shield in Clash Royale: a blow however big breaks it and leaves the
player standing. Ready again in 15 minutes (a minute and a half since §16.72) or at a respawn; 1
in 4 from Skeletron. Its pictures are stand-ins (PlaceholderTextures.md).

### Files

| File | Class | Does |
|---|---|---|
| `Content/Items/Accessories/BoneCasing.cs` + `.png` (26 by 28), `BoneCasingRibs.png` (80 by 168: 40 by 56 frames, behind and in front, three looks) | `BoneCasing`, `BoneCasingSource`, `BoneCasingBag`, `BoneCasingPlayer`, `BoneCasingShards`, `BoneCasingLayer`, `BoneCasingBack`, `BoneCasingFront` | The item; Skeletron's drop and its bag's; the ribs; their pieces; the cage, behind and in front of the wearer |
| `Content/Buffs/BoneCasingBuff.cs` + `BoneCasingBuff.png`, `Splintered.png` | `BoneCasingBuff`, `Splintered` | What is left of the ribs; the wait |
| `Content/Gores/BoneCasingShard1.png`, `2`, `3` | | The pieces |
| `PisAndDag.cs` | `PADPacket.BoneCasing` | What is left, to the other machines |

### What it does

| What | How |
|---|---|
| Takes | every hit something deals (an enemy's touch, a shot, the bosses' holds and drains included), what it would have taken off the wearer after defense, off the ribs' 100 life. Not the world's own hits (`PlayerDeathReason.ByOther`: a fall, lava, spikes, being turned to stone; mine), not another player's |
| The last blow | however big, it breaks the ribs and goes no further |
| How | a hit that can be dodged after the game's own dodges have had their try (`ConsumableDodge`); one that cannot, cancelled where its damage is known (`ModifyHurtInfo`); either way the wearer gets the game's time after a hit that lands (two thirds of a second, a third for a hit of 1, twice with the Cross Necklace) |
| Lasts | until broken or its wearer dies; never mends; saved with the player |
| Again | after Splintered, a minute and a half (§16.72; 15 minutes before); whole at every respawn |
| Seen and heard | the cage from the collarbones to the hips, three looks (whole; cracked at 66 life or less; broken through at 33 or less); a blow: the life it took in bone white, the skeleton hit, bone dust the way it pushed, a flash and a shake; the break: the skeleton death and a crack, bone dust, seven rib pieces flying; standing again: dust drawn in, the rattle of skeletons rising, the cage growing in over six tenths of a second; the slot's eye hides the cage |
| Buffs | Bone Casing while it stands, showing what is left (no time, cannot be clicked off); Splintered, the wait |
| Drop | Skeletron, 1 in 4: from Skeletron in classic, from its bag in expert and master |
| Worth | Green, sells for 1 gold (mine) |
| Tooltip | what it does, three lines; *'It held a heart once. It is not fussy about whose.'* |

### Open

- Never worn in the game, never tried with a second player.
- The pictures are stand-ins; the bone shows less over light clothes. What the ribs take, and
  what was mine: RELEASE_TODO.

---

# 6 · Instruments

## Trumpet

Held and played (`Content/Items/Instrument/Trumpet.cs`). Its recipe, by request (PADWorkFlow
§16.75): *"Trumpet :any gold 3, any iron 6, any copper 14 at anvil"*.

| What | How |
|---|---|
| Recipe | 3 Any Gold Bar (Gold, Platinum), 6 Any Iron Bar (Iron, Lead), 14 Any Copper Bar (Copper, Tin), at an Iron or Lead Anvil; live (mine: the recipes beside it are) |
| The groups | Any Gold Bar and Any Copper Bar are the mod's own (`PADRecipes.AnyGoldBar`, `AnyCopperBar`), named as the game names its Any Iron Bar; the game has a group for iron bars alone |

### Open

- The recipe never made in the game.

---

# Conventions

- A texture sits beside its `.cs`, named identically; the path derives from the
  **namespace**, which mirrors the folder.
- Glow layers are a separate `…Glow.png` on transparency.
- No `.png` is needed where a projectile borrows a vanilla texture and returns false from
  `PreDraw`.
- Item sprites are single-frame unless registered in `Main.itemAnimations`.
