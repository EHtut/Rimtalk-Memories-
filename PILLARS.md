# Pillars

The work, cut into pieces that can be finished one at a time.

**This file owns the decomposition** — what each pillar covers, what it deliberately does not,
what it depends on, and how we know it is done. It records no progress: that is
[STATUS.md](STATUS.md), which tracks against the pillar IDs below. What each feature actually
*is* lives in [DESIGN.md](DESIGN.md).

## How we work through it

One pillar at a time, in order, except where the graph says two are independent.

A pillar is **done** when its exit criteria are observable in a running game — not when the code
compiles. "Compiles" has already misled us once: everything in Pillar 1 built cleanly on day one
and still has never been loaded into RimWorld.

Each pillar ends by updating [STATUS.md](STATUS.md) and, if the design moved, [DESIGN.md](DESIGN.md).
Same session. A pillar that ships with stale docs is not finished.

## Order

```
P1 Foundation
     │
     ├──────────────┬──────────────┐
     ▼              ▼              ▼
P2 Voice & lore   P3 Earshot   (P6 Dialogue templates — after P2)
     │              │
     └──────┬───────┘
            ▼
      P4 Memory core
            │
            ▼
      P5 Recall

P7 Actions — blocked on reference material, unscheduled
```

**A change from the original plan.** The first pass put memory before distance. It should be the
other way round, and the reason is P4: writing a memory means deciding *who witnessed the thing*.
That is the same earshot question P3 answers. Build P3 first and P4 gets witness detection for
free; build P4 first and it needs a crude proximity check that P3 then has to tear out. Ordering
above reflects that. Say so if you would rather have memory sooner and take the rework — it is a
real trade, not a blocker.

---

## P1 — Foundation

**Goal:** a trustworthy seam onto RimTalk, and the ability to see what we are sending it.

**Depends on:** nothing. **Design:** [DESIGN.md](DESIGN.md) §2, §3.
**Working spec:** [docs/specs/P1-foundation.md](docs/specs/P1-foundation.md) — how to do the
remaining tasks.

### In scope
The `RimTalkApi` seam, startup vs game-load registration, settings plumbing, logging, conflict
detection against the mods being replaced, and a debug view of what this mod contributed to the
last prompt.

### Out of scope
Any actual context content. P1 is the pipe, not what flows through it.

### Tasks
- [x] `RimTalkApi` seam — every RimTalk call in one file
- [x] `ContextRegistrar` — idempotent startup registration
- [x] Settings class and settings window
- [x] Prefixed logging with warn-once on the provider path
- [x] Conflict warning for superseded mods
- [ ] **Load it in RimWorld and confirm the three anchors actually fire.** Not all context
      categories are wired through RimTalk's hook path — see
      [docs/RIMTALK-API.md](docs/RIMTALK-API.md) §2. Verify `Pawn:age`, `Pawn:gender`,
      `Environment:time` individually.
- [ ] Debug window: last prompt built, with this mod's contributions marked and their character
      cost shown
- [ ] Decide who owns the total token budget ([DESIGN.md](DESIGN.md) §11)

> The debug window is the highest-leverage item here. Every pillar after this one is tuning text
> that goes into a prompt, and tuning what you cannot see is guesswork. Build it before P2 needs it.

### Exit criteria
1. Mod loads with no errors and the startup log lists the registered providers.
2. RimTalk's prompt output visibly contains a line this mod injected, at each of the three anchors.
3. The conflict warning fires (four superseded mods are installed on this machine, so it should).

---

## P2 — Voice and lore

**Goal:** the player can author how pawns sound and what they all know, and see it land.

**Depends on:** P1. **Design:** [DESIGN.md](DESIGN.md) §4, §5, §6.

### In scope
Age voice, gender voice, world lore, colony lore and its scoping, prompt-preset entries, and a
starter preset worth shipping.

### Out of scope
Anything conditional on the moment — that is templates (P6). P2 is standing context: true of the
pawn or the world regardless of what is happening.

### Tasks
- [x] Age voice, five bands, player-editable, empty-adult default
- [x] Gender voice, off by default with empty text
- [x] World lore, map-wide, character-capped, word-boundary trim
- [x] `{{pawn.ageband}}` template variable
- [ ] Colony lore, and the eligibility rule that keeps it from prisoners and visitors
      ([DESIGN.md](DESIGN.md) §11 — open question: does that check live in context or memory?)
- [ ] Per-save lore override, so one colony's history is not global
- [ ] Prompt-entry registration in a `GameComponent` — the second registration moment
      ([DESIGN.md](DESIGN.md) §3). First code that needs it.
- [ ] A shipped starter preset
- [ ] Settings UI pass once the field count grows past what one scroll pane carries well

### Exit criteria
1. A child, an adult and an elder in the same colony demonstrably speak in different registers.
2. World lore appears once per prompt, not once per participant.
3. A prisoner does not speak from colony-internal knowledge.
4. Turning a feature off removes its text from the next prompt — verified in the debug window.

---

## P3 — Earshot and delivery

**Goal:** who can hear what, and the difference between a whisper and a shout.

**Depends on:** P1. **Design:** [DESIGN.md](DESIGN.md) §9, §10.

### In scope
One earshot model, the distance settings on top of it, response types (whisper / shout / thought)
and their display, and separate length caps for monologue and conversation.

### Out of scope
Using earshot to decide who witnessed an event — that is P4 consuming this, not P3 building it.

### Tasks
- [ ] **The earshot model first.** One function that answers: can B perceive A saying X from
      distance D, through this wall, at this volume? Everything else in this pillar is a caller.
      Get this interface right and P4 inherits witness detection.
- [ ] Distance settings: talk, hearing, viewing, announcement, context radius, same-room toggle.
      Distance Control's defaults (20 / 10 / 20 / 30 / 5 / on) are a sane start.
- [ ] Harmony patches for pawn selection — `PawnSelector.GetNearbyPawnsInternal`,
      `CustomDialogueService.CanTalk`, `ContextHelper.CollectNearbyContext`. Log each one in
      [DESIGN.md](DESIGN.md) §10 as it lands.
- [ ] Suppress the "slighted" social debuff when a pawn was merely out of earshot. Distance
      Control patches `MemoryThoughtHandler.TryGainMemory` for this — a good catch worth keeping,
      and players will notice its absence.
- [ ] Response types as this mod's own concept. `TalkType` is a closed enum
      ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §7), so these cannot be new members.
- [ ] Response type drives the prompt: tell the model it is a whisper before it writes one.
- [ ] Response type drives display and audience — a thought has an audience of one.
- [ ] Monologue and conversation length caps, separately.

### Exit criteria
1. Two pawns in separate rooms do not start a conversation with the same-room setting on.
2. A whisper reaches its target and nobody else; a shout carries further than normal speech.
3. A thought is visible to the player and to no pawn.
4. Being out of earshot does not generate a snub debuff.
5. Monologues and conversations respect their own separate caps.

---

## P4 — Memory core

**Goal:** things that happen get remembered, with weight, and survive a save/load.

**Depends on:** P1, P3 (for witness detection). **Design:** [DESIGN.md](DESIGN.md) §8.1–§8.3.

### In scope
The memory record, what writes one, significance at write time, decay, pinning, persistence, and
a way to look at a pawn's memories.

### Out of scope
Getting memories back out and into a prompt. That is P5. P4 ends with a correct, inspectable
store that nothing reads yet.

### Tasks
- [ ] Memory record type and its `ExposeData`
- [ ] Persistence in a `GameComponent`, and the caravan case — a pawn who leaves and returns keeps
      theirs ([DESIGN.md](DESIGN.md) §11)
- [ ] Capture sources: RimTalk conversations, colony events, battle and social log. Decide what is
      worth a memory at all — this is the difference between a life and a diary of every meal.
- [ ] Witness detection via P3's earshot model
- [ ] Significance scoring at write time, from event kind, mood swing, and closeness to those
      involved
- [ ] Decay with a half-life that scales with significance
- [ ] Pinning, exempt from decay
- [ ] Bounded per-pawn candidate set, kept in decayed-significance order — the thing that makes
      P5 affordable
- [ ] Dev window: inspect one pawn's memories with scores and ages

### Exit criteria
1. A raid, a death and a shared meal all produce memories, ranked in that order by significance.
2. Memories survive save, quit and reload.
3. A pawn sent on a caravan and returned still has theirs.
4. Trivia has visibly faded after a season; a death has not.
5. The candidate set stays bounded in a long-running colony.

---

## P5 — Recall

**Goal:** the right memories, at the right moment, with the ones that belong together arriving together.

**Depends on:** P2 (injection), P4 (the store). **Design:** [DESIGN.md](DESIGN.md) §8.1, §8.3, §8.4.

### In scope
Relevance scoring, retrieval within the tick budget, chained recall, and injecting the result.

### Tasks
- [ ] Relevance scoring against the present moment: participant overlap, proximity, topical match
      against job / thoughts / active events, emotional congruence
- [ ] Retrieval — combine decayed significance with relevance, take top N above a floor
- [ ] **Stay inside the tick budget.** Providers run on the main thread, inline
      ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §4). Measure it; do not assume it.
- [ ] Memory links: same originating event, shared participants, causal follow-on
- [ ] Chained recall at a lower score bar than primary selection, with depth and total capped
- [ ] Inject into pawn context
- [ ] Dev window: for the last recall, show what was chosen and the score breakdown that chose it

> The score-breakdown view is not optional polish. Significance, relevance, decay and chain bar
> are four interacting knobs, and tuning four knobs blind does not converge.

### Exit criteria
1. A pawn brings up a relevant past event unprompted, in a fitting moment.
2. A pawn recalls a raid and the person they lost in it together, not as two unrelated lines.
3. The same memory does not resurface every conversation.
4. No measurable frame cost when a pawn speaks in a long-running colony.

---

## P6 — Dialogue templates

**Goal:** beats the player wants to happen on purpose.

**Depends on:** P2. **Design:** [DESIGN.md](DESIGN.md) §7 — thin, needs real design work first.

### In scope
Authored templates with trigger conditions and slots filled from game state, plus an editor.

### Notes
No prior art to absorb. RimTalk Dialogue Patch is a UI mod for RimTalk's talk window despite the
name, so nothing here comes for free.

RimTalk Custom Events already solves a close problem with JSON-authored multi-phase events. Reuse
that format rather than inventing a second one — and consider whether this pillar belongs in that
mod instead of this one.

### Tasks
- [ ] Design it properly, in [DESIGN.md](DESIGN.md) §7, before any code
- [ ] Decide: reuse Custom Events' JSON shape, or a new format, or move the feature there entirely
- [ ] Trigger conditions and slot filling
- [ ] Authoring UI

### Exit criteria
1. A player-authored template fires on its trigger and reads naturally.
2. It can be authored without editing a file by hand.

---

## P7 — Actions

**Goal:** unknown until the reference lands.

**Depends on:** reference material. **Design:** none yet — this is the acknowledged gap.

The original brief asked for "a better action system with smart action type retrieval". That
reads two ways and they are different projects:

1. **Dialogue drives the game** — what a pawn says produces a RimWorld outcome: a job, an
   interaction, a mood effect, a relationship change.
2. **Better action selection inside RimTalk** — RimTalk already classifies responses
   (`InteractionType`: None / Insult / Slight / Chat / Kind). This would be choosing among those
   more intelligently, scored the way memory retrieval is scored.

Ethan is supplying an actions mod to `references/`. Read it first — it should settle which
reading is meant — then write the design and come back here to fill this pillar in properly.

### Tasks
- [ ] Read the reference once it lands
- [ ] Settle which of the two readings this is
- [ ] Write the design in [DESIGN.md](DESIGN.md)
- [ ] Re-scope this pillar and place it in the order above
