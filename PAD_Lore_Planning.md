# PisAndDag — Lore Planning

> Companion to `Terraria_Mod_Lore_Bible_ver_1.1.md`.
> § numbers follow the Bible's seven-part arrangement; the old numbers map in its appendix.
> The Bible holds what is settled. This holds what is not: open decisions, drafts waiting on approval, and the implementation order.
> Nothing here is canon until it moves into the Bible.

---

# 1. Open decisions

Ordered by how much else is waiting on them.

## 1.1 Names for Life and Birth

Still being decided.

The existing gods establish a system worth keeping: **gods are named in whatever tongue found them.** Mors is Latin, Lôi is Vietnamese. Different peoples, different names, no single divine language.

Candidates:

| God | Option | Source | Note |
|---|---|---|---|
| Life | **Vita** | Latin | The exact counterpart to Mors. Mors/Vita is the classic pair, which is either the point or too neat. |
| Life | **Sinh** | Vietnamese | Life/birth. Pairs with Lôi. Overlaps Birth's domain, which may be a problem or a deliberate blur. |
| Birth | **Ortus** | Latin | A rising, an origin, a beginning. Not obvious, and does not sound like a name, which suits her. |
| Birth | **Genitrix** | Latin | Begetter, mother. Heavier, more mythological. |
| Birth | *(none)* | — | A god of beginnings who refuses to settle into a noun. |

**If Birth stays unnamed, she still needs a fixed epithet**, because the lore-voice skill currently has a blank where her name goes and NPCs have to call her something. Candidates: *the mother of too much*, *she who does not stop*, *the one who begins*.

## 1.2 The Pilgrim's name

**Resolved → Bible §XXXI: Pugrim.**

## 1.3 Where the Judge sits

The Low Court's architect. The location sets the whole faction's texture and nothing else can be written until it is picked.

- **A chair in the Dungeon** — the order is grand, old, and hidden behind a boss.
- **A bench in town** — the order is among you and always has been. Much worse, in the good way.
- **A room in the Arcanum ruins (§XXIV)** — ties the Court to the Archmage's institution.

## 1.4 Which boss the Pilgrim fights beside

Constraint from §III: he is the only character with no agenda, so do not spend him inside somebody else's plot. That rules out the Megashark, which belongs to the Mechanic.

§XXXI's original note says a major dragon boss. No dragon exists yet.

Whichever it is, this is the fight where he draws the sword (§XXXI) — the only time.

## 1.5 When the Jungle was poisoned

Decides whether the five Low Court members are heirs or perpetrators.

- **Ancient** — they live in the shadow of a crime none of them committed, working from instructions they inherited. More Souls.
- **Recent, a few generations** — at least one of the five was there. More personal, less mythic.

## 1.6 Was the poison manufactured?

**Recommendation, not yet approved.** The Jungle poison should be a *built* thing — pumps, vats, pipes, out of the engineering traditions (§XXI / §XXV) — not a spreading metaphysical rot.

Two reasons. It stops the poison colliding with Corruption and Crimson, which are already the world's infectious absolutes (§XXVI). And it leaves a **ruin**: an apparatus rusting in the deep Jungle with mud grown through it, a place the player can stand inside and work out what happened. Metaphysical forces do not leave machinery. That is how the player tells the two apart.

## 1.7 Loi's third transgression

§IX locks the first two — the orb is the shout, flight is the trespass. The third, whatever is done up there, is open.

## 1.8 Firearms have no origin

**Resolved → Bible §XXI "Firearms".** Stop vs finish; Goblin Artificers first, the Mechanic takes the hand out; no first gun as an object, one Tinkerer line. Still to place: the casings in Low Court ruins, and the Tinkerer line in his dialogue.

## 1.9 Which town NPC is the Low Court follower

**Resolved → Bible §XVII: the Tax Collector.** Still to write: his one line he should not know, and his second when the player carries the Blade or the Orb.

## 1.10 Endings

§XXXII lists seven. That is a great deal of writing. Which are real, and which are flavour?

---

# 2. The Low Court — the five

Sketch. None locked. The shape is the Souls faction spine: a contact, a believer, a doubter, an architect, a casualty — each at a different point on the same belief, none repeating another's facts.

| # | Working name | Tier | Where | Function |
|---|---|---|---|---|
| 1 | The Collector | Periphery | Town | Carries letters, does not open them. Cannot tell the player anything, which is why he is safe to leave in a town the player visits daily. Never confirms. |
| 2 | The Surgeon | Core | Underground | A room of divine anatomy; §XVII's dissections given a face. Cheerful about appalling work. The vendor: anti-divine gear, anchor tools. Believes completely. |
| 3 | The Doubter | Core | Near death-touched places | Has met the tall man in the caves and has been unravelling since. The heretic of §XVII. The faction's only other door for the player. |
| 4 | The Judge | Core | See 1.3 | The architect. Old, unremarkable, soft-spoken. He put the orb where the player would find it. The worst thing in the order. |
| 5 | The One Who Went First | Core | Unknown | Tried an anchor. Something happened. Not hostile so much as unfinished. Probably the only one of the five that is a fight. |

**Scope warning.** Five Souls-style questlines with relocation, world-state gating and silent failure is heavy for tModLoader — NPC placement, per-world flags, missable branches. Recommended split: **full questlines for the Doubter and the Judge**; the other three are fixed encounters whose dialogue changes as world state moves.

---

# 3. The Pilgrim — plan

Locked in the Bible (§XXXI): he wants nothing, he carries a bow, he introduces himself every time, he appears early and often.

## 3.1 First meeting

Two doors, and **the players who fail get the better one.**

**If the player dies to the Eye of Cthulhu:** hold the camera on the body — the rig already exists from the boss animations — and an arrow comes in from off-screen and finishes the Eye. Then the respawn. The player never got to thank him. When they meet him later, he does not mention it.

**If the player beats the Eye themselves:** they simply find him on the road afterwards, with no explanation at all.

## 3.2 Along the way

He turns up at intervals and gives the player what a traveller carries, not what an ally supplies — berries, rum, a coat. Deliberately unhelpful and kind. Nothing good.

## 3.3 The last gift

At the final meeting he hands over the bow, and the player understands he is not coming any further.

The sword goes earlier and differently: after the one fight he draws it in (3.4), it is on the player's back or on his grave — never handed over. He would not give away something that was not his.

## 3.4 Questline

Small. Undecided. One boss fought beside the player (see 1.4).

---

# 4. Implementation order

What the lore now needs from the mod, roughly in the order it becomes buildable.

## 4.1 Already built and waiting on lore

- **Shoreline graveyard** + first-death burial, epitaphs, silence — built. §XXXII.3 now ties the unused open grave to the ending.
- **Camera rig / UI hiding** — built. The Pilgrim's arrow intro (3.1) needs exactly this.
- **TalkOptions** dialogue system + heard-line memory — built. Every NPC branch below runs on it.
- **Ominous Orb / Legends mode** — built. Now means "the heavens are watching" (§XXX); the announcement line and the Guide's Legends line predate the voice and want rewriting.
- **Blade of Mors** — built. §XVIII makes it the most important object class in the world.

## 4.2 Cheap and high value

- **Defaced angel statue** in the generated graveyard — the Low Court's signature, in the world in hour two, decades before the player can name it (§XVII).
- **Mors at the graveyard** after the first death — a wandering encounter, not a boss (§XV).
- **First-respawn line** — done.
- **Truffle and Dryad branches** — done.

## 4.3 Needs design first

- The Low Court's five (section 2).
- The Pilgrim (section 3).
- The Jungle apparatus ruin (1.6).
- A firearm origin (1.8).
- Post-restoration world state: town stops fighting, deaths stick, questlines break (§XIV). Needs a single world flag and a pass over every NPC hook.

---

# 5. Scrapped

- **The crypt under the graveyard** — trapdoor, tunnel, unbreakable block, pitch-dark shader. Previewed and rejected. Do not rebuild.

---

# 6. Re-voicing backlog

Text written before the lore-voice skill existed.

- The orb's break announcement: *"The essence of \<world\> have been stirred"*.
- The Guide's Legends line: *"I feel like the balance of the world is disturbed…"* — now specifically about being watched from above (§XXX), not about general ominousness.
