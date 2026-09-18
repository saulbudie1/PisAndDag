# Greatbow of a Pilgrim — build plan

> The weapon Pugream hands the player after Armageddon. Lore lives in the Lore Bible,
> §XXXI *The Greatbow* and §IX *What He Spends*; this file is the machine.
>
> The mod has **no `modReferences`**, so this is balanced against vanilla and only
> checked for sanity against Calamity. Nothing here may depend on Calamity loading.

---

## 1. What it is in one line

A rebuild of a bow the player watched work, once, in somebody else's hands, before it
broke. It is the best bow in the world and it is worse than the one it was made from, and
the player can see exactly where the difference is.

---

## 2. Stats

| | |
|---|---|
| base damage | **350** |
| damage class | Ranged |
| ammo | **Great Arrows only** (custom, §5) |
| full draw | **6 seconds** (360 ticks) |
| stagger on hit | **5 seconds** (300 ticks) |
| pierce | infinite |
| tile collide | false |
| special | skyward, 8 s charge, 10–15 arrows on a locked point (§6) |

`Item.channel = true`, `Item.shoot` = a holdout projectile, `CanShoot => false`, arrows
fired from the holdout. This is the Calamity house pattern (Drataliornus, Ultima,
Ontological Despoiler all do it) and it is where the draw meter and the draw animation
live.

---

## 3. The draw, and the missing second

**The stagger is 5 seconds. The draw is 6.** The player is always one second short of
being ready when the thing gets up.

Keep it. It is two things at once:

**It is the character.** His bow drew in five. That is why the player watched him stagger
that animal and be ready again, over and over, in the Armageddon fight. The rebuild takes
six. The missing second is the wound, and it is in the player's hands for the rest of the
game.

**It is the entire balance of the weapon.** A 5-second stagger on a 5-second draw is a
permanent lock on every boss in the game, forever. The gap is not a rough edge to file
off — file it off and the weapon is illegal.

**[DECIDE] Diminishing returns.** Even at 5-in-6 a boss spends 83 % of the fight unable to
act. Recommended: repeated staggers on the same NPC shorten — 5 s, then 3, then 2, with a
1 s floor — and the ladder resets after ~15 s without one. The first stagger of an
encounter is always the full five, so the flex moment survives and the lockdown does not.
The alternative is to leave it flat and accept that this weapon is the answer to every
boss.

---

## 4. The arrow

Pierces everything, ignores terrain, and **lodges**. Same shape as
`Content/Projectiles/Melee/BladeOfMorsProj.cs`, with a much longer bite.

```csharp
Projectile.penetrate = -1;
Projectile.tileCollide = false;
Projectile.usesLocalNPCImmunity = true;
Projectile.localNPCHitCooldown = 30;   // see the sums below
```

On hit: stash the velocity, zero it, start a lodge timer, resume when it runs out — the
Blade of Mors `FreezeTimer` / `storedVelocity` pattern exactly, so read that file before
writing this one.

**The lodge lasts as long as the stagger.** Five seconds in, hitting on its cooldown, then
it tears free and carries on to whatever is behind. One object, two effects, and the
player never has to be told they are the same duration because they watch it happen.

### What that actually costs the target

Hits per lodge = 300 / `localNPCHitCooldown`. Cycle is the 6-second draw.

| cooldown | hits | per shot | DPS |
|---|---|---|---|
| 20 | 15 | 5 250 | 875 |
| **30** | **10** | **3 500** | **583** |
| 45 | 6.7 | 2 333 | 389 |

Vanilla's best single-target ranged sits under a thousand. **Start at 30** and tune from
the fight, not from the spreadsheet. This one number is the weapon's whole balance; the
350 is cosmetic next to it.

---

## 5. Great Arrows

A custom ammo item. The bow takes nothing else.

- The arrow: `Item.ammo = ModContent.ItemType<GreatArrow>()`, `Item.shoot` = the lodging
  projectile above.
- The bow: `Item.useAmmo = ModContent.ItemType<GreatArrow>()`.

They are the weapon's economy. One per draw, **ten to fifteen for the rain**, so the
special costs about twelve shots' worth of ammunition and returns about one shot's worth
of damage. That is correct and must stay correct — the rain is a control tool, not a
damage rotation (§6). Anybody who later "fixes" its damage has broken it.

**[OPEN]** Where Great Arrows come from, and whether they are craftable or finite.

---

## 6. The rain

Right-click (or a held alt): he points it at the sky, **charges 8 seconds**, and 10–15
Great Arrows come down on a point **locked before the charge begins**.

- The point is chosen and then fixed. Everything that lands, lands there.
- Each arrow staggers and applies bleed.
- Eight seconds is a very long time to be committed. That is the cost, and it should
  feel like one — no cancelling for a refund.
- Draw a marker on the locked point for the whole charge. The player must be able to
  watch the thing walk out of the box they paid for.

---

## 7. Stagger — implementation

Terraria has no stagger. Build it as a `GlobalNPC` with `InstancePerEntity`.

- A `StaggerTicks` field on the instance.
- `PreAI(NPC npc)` returns **false** while `StaggerTicks > 0`, which skips the NPC's AI
  entirely, and zero `npc.velocity` in the same call. (`GlobalNPC.PreAI`, `AI`,
  `ModifyIncomingHit` and `OnHitByProjectile` all verified present in the staged
  `tModLoader.dll`.)
- Decrement in `AI` or `PostAI` so it still ticks while the AI is skipped.

**Risks, and they are real.** Freezing AI outright breaks bosses whose state machines
assume they run every tick — teleporters mid-teleport, multi-segment worms, anything
driven by a timer in `npc.ai[]` that also gates a despawn. Expect to need a
**blacklist**, and expect Calamity bosses to need their own entries. Test worms first.

### The Wall of Flesh

It is explicitly on the list: **pushed back, and staggered for five seconds.**

The Wall drives its own X position from its AI and speeds up as it loses health, so a
push-back has to write `npc.position.X` and then survive the Wall re-deriving it on the
next tick. Test that the push sticks rather than snapping, and that its two eyes and the
Hungry follow it.

Note what this is: by the time the player owns this bow the Seal is long broken and the
Wall is a fight they can revisit. Nothing needs saying about that. Somebody will do it
once, see it go backwards, and tell everybody.

---

## 8. The full draw the player never gets

Recorded so it does not get lost: the original's advantage was **the second**. Five to
draw instead of six. Not extra damage, not an extra effect — the same weapon, in time.

Everything the player sees Pugream do with it during the Armageddon fight should be
exactly what they can do afterwards, only never quite quickly enough.

---

## 9. Open

- The diminishing-returns decision in §3.
- `localNPCHitCooldown` final value.
- Where Great Arrows come from.
- Whether the rain's bleed is a vanilla debuff or a PAD one.
- The sprite. It is a greatbow made of a wood nobody here works with, restrung with hair,
  and the repair must be visible on it — the mend should be the first thing the eye finds.
