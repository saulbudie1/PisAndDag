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

**Resolved → Bible §XXXI: Pugream.**

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

**Resolved → before the Archmage.** The Court's attempt sits in the age before the Unremembering; everything the Jungle has done since is one uninterrupted reaction.

### Original note

Decides whether the five Low Court members are heirs or perpetrators.

- **Ancient** — they live in the shadow of a crime none of them committed, working from instructions they inherited. More Souls.
- **Recent, a few generations** — at least one of the five was there. More personal, less mythic.

## 1.6 Was the poison manufactured?

**Resolved → yes, built.** Now stated in Bible §XVII.

### Original note

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
- **White Death** — the boss shell and its marked-spot volley are built and unkillable by construction. Section 8; the unwinnable first meeting is already most of the way there in code and has no lore attached to it at all.

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


---

# 7. Opened by the structural pass

The pass that joined the two catastrophes closed a lot and opened these.

## 7.1 Where the correspondence is found

Bible §XXVII puts the Archmage's half in the Arcanum ruins and the houses' half beneath the world. Neither location is designed. The houses' half in particular wants a room the player can stand in.

## 7.2 Does the Clerk's archive feed the restoration mechanically?

§XIV says the ledgers are a copy of the memory that was taken, which makes them useful to Mors. Whether that is a quest item, a long fetch, or purely a thing the player realises is undecided — and it decides whether the Clerk is a character or a dungeon.

## 7.3 Where the Archmage sits in progression

He is locked as alive and as the opposition to the restoration (§XIII). Whether he is the last fight, a late-but-not-last fight, or something that is never fought at all is open. §XIII still carries the old boss proposal, which was written when he was optional.

## 7.4 What the Low Court does about Nine Tenths

§XIV says how the player treats him is how the Court takes an interest. The mechanism — whether they ask, take, or simply watch — is unwritten.

## 7.5 Loi's third transgression

Unchanged from 1.7, and now the only major undecided item left in Part II. The proposal on the table is that the third transgression is asking him something.

## 7.6 The seven endings still have no levers

§XXXII lists seven and most have no stated trigger. They are also not commensurate. Three or four with a concrete lever each is the recommendation.


---

# 8. The Sniper — plan

New. Nothing in the Bible yet. The shell is already in the mod.

**Built:** `Content/NPCs/WhiteDeath/WhiteDeath.cs` — 500 life, `boss = true`, `AutoloadBossHead`, and both `CanBeHitByItem` and `CanBeHitByProjectile` returning **false**. He fires `Marksman_WhiteDeath` every 45 ticks at a point rolled inside the player's own hitbox, with the offset stashed in `ai[0]`/`ai[1]`. The unwinnable first meeting and the marked-spot telegraph exist as code. None of what follows does.

## 8.1 What he is

The best shot in the world. The Low Court want him and have never had him. He lives alone, and the word for it is not recluse — a sniper does not hide, he holds a position. He lives where he lives because it is the furthest point from which he can still cover a particular door.

He is **not one of the five** (section 2). He is the one they never got, which keeps the five intact and makes him a standing failure of theirs.

## 8.2 The first meeting

Three shots, to 1 life. The report arrives **after** the wound — land all three, then play the sound, from a bearing that does not help.

Then a debuff that stops regeneration. Working name **Held**: he is holding the player still, and he is holding his fire. It reads twice, it is plain, and the mod has no other debuff by that name.

The debuff is the point of the scene. He fired three times and stopped, and the wound not closing is what makes stopping legible as a decision rather than as mercy. Three casings left on the ridge, pickable — §XXI makes the casing a receipt for a decision, and leaving the accounting where the player will find it is the character in one object.

## 8.3 Why he turns the Court down

§XVII: they have never killed a god. §XXI: the gun's verb is **stop**, not finish — only a Death Dagger ends anything (§XVI). So a rifle is no use to them against gods at all.

What they actually want him for is **the mortals in the way**: witnesses, couriers, the Doubter when he finally says it out loud.

So the refusal is not moral. He is an instrument with one function and they are asking him to be a different one. Accuracy as ethics, which is cold enough to be him.

It also hooks their own structure: §XVII says the periphery carry letters and never ask what is in them. He is the one man who was sent a letter and did not open it.

## 8.4 Why he cannot do it himself

This is the load-bearing question of the whole questline and §XXI already answers it, in the Goblin Tinkerer's one line about the first gun:

> *"The first one wasn't for killing. It was for a thing you couldn't walk up to. Then somebody walked up to something anyway."*

He is the man who cannot walk up. The player is the somebody who does.

Every gun in the mod takes something from its wielder (§XXI). His takes **proximity**. He has had that door in a scope for years and the scope is the whole problem.

## 8.5 The wife

A duchess of the Kingdom of Grace, blessed by the gods and by Shimmer.

§XIX is explicit that Grace's good years fall **before** the Archmage's lock: the kingdom was drinking the live thing, and whatever the bloodline became it actually became. So she is not a woman who was told she was blessed. She was changed, back when Shimmer still asked its question — and she is therefore **the only surviving thing in the world that the working Shimmer altered.** Every other trace the player will ever touch is §XI's disarmed water.

Which means the Low Court are holding the one live sample of the antidote to what was done to Mors (§XI), in jars, filed under anatomy.

That is §XVII's error run again at full scale. They poisoned the Jungle to wound a god, got Plantera, and read a reaction as a vulnerability. Here they took a woman apart hunting for an anchor, and what is in the jars is the cure they are not looking for. They will never find it, because they are measuring the wrong thing.

**She was taken as a vessel, not as leverage.** §XVII lists kidnapping divine vessels among their methods. If they took her to buy him, the Court becomes a villain in a story about bureaucrats. They took her for the work and noticed whose wife she was afterwards, in their own ledger. The letters start after she is already inside.

**The letters are the entire fee.** Each one says she is alive. Each one is true. *The Court has never lied to him.*

And nobody at the door is hateable. §XVII: the periphery hold no doctrine, only a debt — somebody hid their brother once, so they keep a drawer. The man who turns her key has no opinion about gods.

## 8.6 The jars

They are not remains. Under the amnesty (§XIV) nothing ends, so taking a person apart is not killing, it is disassembly, and it does not conclude. She is in them.

The cell is not the sentence. The continuing is — and that is this world's horror specifically, because the exit every prisoner in history has had is the one the Archmage closed.

Deliver it through the Court's own accounts rather than a narrator (§XVII: they keep accounts), in the inscription register — fragments, clinical hand:

> `VII — the eye. Still answers to light.`
>
> `XI — opened again. No change.`

The player should work out what *no change* means about a page later.

## 8.7 What "free her" can mean — **OPEN**

His request no longer means what he thinks it means. Three routes, and this has to be decided deliberately rather than discovered late:

1. **Carry the jars out.** She persists, scattered, aware. He gets her back and there is no her to get back.
2. **A Quietus.** §XVI and §XVII: a Death Dagger is the only object left that can end anything, the Court has wanted one for centuries and never held one, and the first Quietus is what turns them toward the player. Freeing her means killing her, with the one tool they hunt, inside their own house. Cruellest and most structurally loaded — it makes his request and their founding grievance the same act.
3. **Unlock Shimmer.** §XI: the lock is the Archmage's second crime, and live Shimmer asks *what if you were something else*. She is already something else. The only route where she comes back whole, and it costs the world its disarmament. `PADWorld.ArchmageShimmer` and `/archmageshimmer` already exist.

**Recommendation:** he asks for 1, what is actually available is 2, and the weight of the scene is whether the player tells him.

## 8.8 The restoration collision — **DECIDE EARLY**

§XXXII.3: when Mors remembers, everyone living on the amnesty stops. Restoring death may be what opens her door, and it is not what he asked for.

Same mechanism as the Mechanic's death (§XXI), which is the risk — it would be the second time the book plays that card. Pointed at a man who spent the whole game asking for the opposite, it may earn the repeat. Or it may cheapen hers.

## 8.9 Collisions to respect

- **§XXXI Pugream is the one who wants nothing**, and the Bible says that is his entire function. The sniper must want something small and specific — to not be aimed, to be left alone — never nothing.
- **§XX the Princess** is Grace's surviving remnant who privately doubts the kingdom was ever blessed. The duchess is the proof that it was, and the proof is what it cost. They pair rather than collide. If it ever reaches her, *A Small Life* gets a shadow across it.

## 8.10 Voice

Cold, plain, declarative. Short. No hedging and no metaphor, which sets him against Mors's lost grammar and against the Pilgrim's warmth.

He never says *wife* with weight on it, and not first. The first mention is a door, a count, a schedule, and the player assembles the rest.

Lines on the table:

> I can reach anything I can see. I have never been inside.

> They send letters. Every one says she is alive. That is all they are buying.

> Ask them how many they are holding. They will give you a number. It is short by one.

> Walk in. I cannot.

## 8.11 Open

- Is **White Death** his name, or what other people call him?
- Does he ever see the jars himself, or only hear what the player chooses to say?
- Can the player lie to him about what they found?
- What exactly does the rifle take, and is it stated or only shown?
- Does he fire for the player once, ever? If he does, the Court learns of it — the same shape as the first Quietus and the Orb alerting the Sky.
- Is he a boss in the bar sense at all? He is currently `boss = true` with a head icon and no way to hurt him, which is either a bug or the joke.

---

# 9. The Clanmaster — plan

New. Nothing in the Bible and nothing in the mod yet. **Clanmaster** is a working name, taken from his sword.

## 9.1 Decided

- A knight: a medieval swordsman with a straight sword, not a katana. Very strong and fast. He deflects projectiles, dodges, and parries.
- His clan was destroyed half a year before the Working (the Archmage's act, §XIII; the Timeline's year zero). He went to kill the head of the clan that did it, and by then death no longer held: the man survived being killed. So he trapped him, to kill him once and for all when it could be done.
- **The questline is the restoration.** The player helps him make Mors remember (§XXXII.3).
- Met again after Mors remembers, he has killed his prisoner. He has lived too long, and what he wants now is a warrior's death, a deserved one. He asks the player for it.
- **An optional boss.** Post–Moon Lord, and he can be stronger than the Archmage.
- **The end of the fight is a choice.** When he is bleeding out, the player can:
  - **kill him**: his wish is granted. No sword. An achievement, **Wish Maker**.
  - **spare him**: the player gets the sword, and he dies dissatisfied.
- He is built to be likable, so players want to give him the death he asks for.
- The choice is offered by a very small picture in a corner of the screen, over the cinematic, drawing as little attention as it can.

### The sword: Clanmaster's Corpse Piler

One side rusted, one side clean. He fights the player with the clean side.

## 9.2 Proposed in discussion, not decided

- **§XXXII.3 gives the motive.** When Mors remembers, "everyone who had been living on the amnesia now goes". A man three hundred years old is exactly that. So his death is coming whatever the player does, and what he wants is to choose its kind: a sword, not a bed. The spare branch then needs nothing special: the player walks away and death takes him the dull way, which is why he dies dissatisfied. (Tension to settle: if the restoration takes the overstayed at once, he cannot be met after it. He has to be going, not gone.)
- **Kill is the Quietus.** §XVI: after the restoration the Blade of Mors ends what is already dying, in the last sliver. The last minute of this fight is that sliver. §XVI also says the first Quietus is what turns the Low Court toward the player: decide whether this may be the first, or must not be.
- **The sword reads twice.** The rusted side did three hundred years of killing on one man who kept getting up: §XIV's recut stone, in steel. The clean side has not been used since, because nobody was worth it. Fighting the player with it is the compliment.
- **The picture in the corner:** the Blade of Mors item sprite, about 24 px, faint, pulsing slowly. Kill is using the Blade while it is up; spare is doing nothing until it fades (8 to 10 seconds), so not choosing is the choice. Without the Blade: straight to spare, or the fight is gated on carrying it. Undecided.
- **His part in the restoration is self-interest.** The prisoner cannot be ended until death works, so he helps. He may know where the Clerk is: the Clerk's archive is a copy of Mors's lost memory (§XIV).
- **Likable, concretely:** courteous, dryly funny, generous with advice, three centuries good at patience. He treats the player as a peer rather than a tool, which sets him beside Pugream (§XXXI) in the book's structure. The prison is never shown before the restoration: players like him first and learn what his patience was for afterwards.
- **Where his dead are:** the clan's battlefield can be one of §XIV's un-rotted fields, the bodies still lying where they fell.
- **The mirror:** his clan died before the Working, so theirs was one of the last killings that held; his enemy's came after, and did not. The same line Bustworth's patricide stands on (§XIX).
- **Achievements:** the installed tModLoader has a `ModAchievement` type (members not yet checked). Keep any system small: a handful for choices like this one, not a kill list.

## 9.3 Open

- Who the two clans are: families of Grace, a people outside the kingdom, or two sides of the goblin war's siege line (§XXI)? Not "houses": the Bible already uses that for the learned houses (§XXVII).
- Did the enemy head know the Working was coming, and time the massacre? Too close to Bustworth if confirmed; better suspected and never answered.
- The withheld fact: what the prisoner did, or what the Clanmaster's own clan did first. A revenge story is stronger if the player can never be sure who began it.
- His name. Where he is met before the restoration, and where the prison is.
- Can he be killed without the Blade of Mors?
