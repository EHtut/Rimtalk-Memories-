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

**But loading RimWorld costs ~20 minutes**, so there is exactly one verification run per pillar,
at the end. Three rules follow, and they shape how every pillar is planned:

- **Build the instrument before running the experiment.** Whatever is needed to read the result
  must exist before the run, or the 20 minutes buys one answer instead of twenty.
- **Push everything possible to the main menu.** RimWorld loads mod settings at startup, so a
  panel in Mod Options is inspectable without loading a save. Anything checkable there should be
  checked there.
- **Run `tools/smoke-test.ps1` before every verification run.** It drives the built assembly by
  reflection with no game loaded. It already caught a `MissingMethodException` that would have
  presented in game as a dead anchor and cost a second load chasing the wrong cause.

Each pillar ends with a single explicit test checklist, and by updating [STATUS.md](STATUS.md) and,
if the design moved, [DESIGN.md](DESIGN.md). Same session. A pillar that ships with stale docs is
not finished.

## Order

```
P1 Foundation
     │
     ├──────────────┬──────────────┐
     ▼              ▼              ▼
P2 Voice & lore   P3 Earshot   P7 Dialogue templates (after P2)
     │              │
     └──────┬───────┘
            ▼
      P4 Memory core
            │
            ▼
      P5 Recall ──────► P6 Smart context (shares the relevance scorer)
            │
            ▼
      P8 Actions ── blocked on reference material

P9  Literature & quests   — independent of everything above, see below
P10 Mod integration       — after P2
P11 RJW compatibility     — last, after P3
```

**P9 does not queue behind anything.** Books and quests are not conversations: they use RimTalk's
AI client directly rather than its prompt pipeline ([DESIGN.md](DESIGN.md) §14), so they touch none
of the context work. Pull it forward whenever the context pillars need a break.

**P3 before P4** is deliberate and was a change from the first plan. Writing a memory means
deciding who witnessed the thing, which is the same earshot question P3 answers. Build P3 first and
P4 inherits witness detection; build P4 first and it needs a crude proximity check that P3 then
tears out.

---

## P1 — Foundation

**Goal:** a trustworthy seam onto RimTalk, and the ability to see what we are sending it.

**Depends on:** nothing. **Design:** [DESIGN.md](DESIGN.md) §2, §3, §12.
**Working spec:** [docs/specs/P1-foundation.md](docs/specs/P1-foundation.md).

### In scope
The `RimTalkApi` seam, startup vs game-load registration, settings plumbing, logging, conflict
detection, the character budget, and a panel showing what this mod contributed.

### Out of scope
Any actual context content. P1 is the pipe, not what flows through it.

### Tasks
- [x] `RimTalkApi` seam — every RimTalk call in one file
- [x] `ContextRegistrar` — idempotent startup registration
- [x] Settings class and settings window
- [x] Prefixed logging with warn-once on the provider path
- [x] Conflict warning for superseded mods
- [x] Injection profile panel in Mod Options, reachable from the main menu
- [x] Per-anchor mode — injected before/after, folded in, or replacing RimTalk's text
- [x] Character budget, priority-ordered ([DESIGN.md](DESIGN.md) §12)
- [x] `tools/smoke-test.ps1` — drives the assembly with no game loaded
- [ ] **The one verification run** — load once, work the whole checklist in the spec

### Exit criteria
1. The panel opens **from the main menu** and correctly shows RimTalk's native preset, our layer,
   every text variant, and the budget allocation — with no save loaded.
2. Mod loads with no errors and the startup log lists the registered providers.
3. Every registered section shows a non-zero call count, and the assembled prompt contains its
   text at the intended position.
4. World lore appears once per prompt, not once per participant.
5. The conflict warning fires.

---

## P2 — Voice and lore

**Goal:** the player can author how pawns sound and what they all know, and see it land.

**Depends on:** P1. **Design:** [DESIGN.md](DESIGN.md) §4, §5, §6.

### In scope
Age voice, gender voice, world lore, colony lore and its scoping, prompt-preset entries, and a
starter preset worth shipping.

### Out of scope
Anything conditional on the moment — that is templates (P7). P2 is standing context.

### Tasks
- [x] Age voice, five bands, player-editable, empty-adult default
- [x] Gender voice, off by default with empty text
- [x] World lore, map-wide, character-capped, word-boundary trim
- [x] `{{pawn.ageband}}` and `{{worldlore}}` template variables
- [ ] Colony lore, and the eligibility rule that keeps it from prisoners and visitors
- [ ] Per-save lore override, so one colony's history is not global
- [ ] **Prompt-entry registration in a `GameComponent`** — the second registration moment
      ([DESIGN.md](DESIGN.md) §3.2), and the first use of mechanism 3 from §2.1: a block we place
      ourselves, anywhere in the message list, filled per prompt
- [ ] A shipped starter preset
- [ ] Settings UI pass once the field count outgrows one scroll pane

### Exit criteria
1. A child, an adult and an elder in the same colony demonstrably speak in different registers.
2. World lore appears once per prompt, not once per participant.
3. A prisoner does not speak from colony-internal knowledge.
4. Turning a feature off removes its text from the next prompt — verified in the panel.

---

## P3 — Earshot and delivery

**Goal:** who can hear what, and the difference between a whisper and a shout.

**Depends on:** P1. **Design:** [DESIGN.md](DESIGN.md) §9, §10.

### In scope
One earshot model, distance settings on top of it, response types (whisper / shout / thought) and
their display, and separate length caps for monologue and conversation.

### Out of scope
Using earshot to decide who witnessed an event — that is P4 consuming this, not P3 building it.

### Tasks
- [ ] **The earshot model first.** One function: can B perceive A saying X from distance D, through
      this wall, at this volume? Everything else here is a caller, and P4 inherits it.
- [ ] Distance settings: talk, hearing, viewing, announcement, context radius, same-room toggle.
      Distance Control's defaults (20 / 10 / 20 / 30 / 5 / on) are a sane start.
- [ ] Harmony patches for pawn selection — `PawnSelector.GetNearbyPawnsInternal`,
      `CustomDialogueService.CanTalk`, `ContextHelper.CollectNearbyContext`. Log each in
      [DESIGN.md](DESIGN.md) §10 as it lands.
- [ ] Suppress the "slighted" social debuff when a pawn was merely out of earshot — Distance
      Control patches `MemoryThoughtHandler.TryGainMemory` for this, and players notice its absence.
- [ ] Response types as our own concept. `TalkType` is a closed enum
      ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §7), so these cannot be new members.
- [ ] Response type drives the prompt: tell the model it is a whisper before it writes one.
- [ ] Response type drives display and audience — a thought has an audience of one.
- [ ] Monologue and conversation length caps, separately.

### Exit criteria
1. Two pawns in separate rooms do not start a conversation with same-room on.
2. A whisper reaches its target and nobody else; a shout carries further than normal speech.
3. A thought is visible to the player and to no pawn.
4. Being out of earshot does not generate a snub debuff.
5. Monologues and conversations respect their own separate caps.

---

## P4 — Memory core

**Goal:** things that happen get remembered, with weight, and survive a save/load.

**Depends on:** P1, P3. **Design:** [DESIGN.md](DESIGN.md) §8.1–§8.3.

### In scope
The memory record, what writes one, significance at write time, decay, pinning, persistence, and a
way to look at a pawn's memories.

### Out of scope
Getting memories back out and into a prompt — that is P5. P4 ends with a correct, inspectable store
that nothing reads yet.

### Tasks
- [ ] Memory record type and its `ExposeData`
- [ ] Persistence in a `GameComponent`, and the caravan case
- [ ] Capture sources: RimTalk conversations, colony events, battle and social log. Decide what is
      worth a memory at all — the difference between a life and a diary of every meal.
- [ ] Witness detection via P3's earshot model
- [ ] Significance at write time, from event kind, mood swing, and closeness to those involved
- [ ] Decay with a half-life that scales with significance
- [ ] Pinning, exempt from decay
- [ ] Bounded per-pawn candidate set in decayed-significance order — what makes P5 affordable
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

**Depends on:** P2, P4. **Design:** [DESIGN.md](DESIGN.md) §8.1, §8.3, §8.4.

### Tasks
- [ ] Relevance scoring against the present moment: participant overlap, proximity, topical match
      against job / thoughts / active events, emotional congruence
- [ ] Retrieval — decayed significance combined with relevance, top N above a floor
- [ ] **Stay inside the tick budget.** Providers run on the main thread, inline
      ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §4). Measure it; do not assume it.
- [ ] Memory links: same originating event, shared participants, causal follow-on
- [ ] Chained recall at a lower score bar than primary selection, depth and total capped
- [ ] Inject via a prompt entry and a registered context variable — mechanism 3, so recall lands
      where we choose rather than beside whatever anchor is nearest
- [ ] Panel view: for the last recall, what was chosen and the score breakdown that chose it

> The score-breakdown view is not optional polish. Significance, relevance, decay and chain bar are
> four interacting knobs, and tuning four knobs blind does not converge.

### Exit criteria
1. A pawn brings up a relevant past event unprompted, in a fitting moment.
2. A pawn recalls a raid and the person they lost in it together, not as two unrelated lines.
3. The same memory does not resurface every conversation.
4. No measurable frame cost when a pawn speaks in a long-running colony.

---

## P6 — Smart context

**Goal:** send what matters now, instead of everything every time.

**Depends on:** P5 (shares the relevance scorer). **Design:** [DESIGN.md](DESIGN.md) §13.

### In scope
Scoring RimTalk's own context fragments for relevance to the moment, and spending the budget on
what scores highest. Turns §12's allocation from static into per-prompt.

### Tasks
- [ ] Reuse P5's relevance scorer against context fragments rather than memories — one scorer, not two
- [ ] `Override` hooks that hand back filtered versions of RimTalk's categories (thoughts, social,
      health, surroundings) instead of its full dumps. No Harmony needed; Context Upgrade patched
      `ContextBuilder` for this and did not have to.
- [ ] Per-prompt allocation, cheap enough for the tick path — bounded candidates, precomputed scores
- [ ] Panel view: what was dropped this prompt, and why

### Exit criteria
1. A pawn arguing about dinner does not carry their full medical history into the prompt.
2. A pawn who was just shot does.
3. Measurable reduction in characters sent, with no loss of the text that mattered.
4. No measurable frame cost.

---

## P7 — Dialogue templates

**Goal:** beats the player wants to happen on purpose.

**Depends on:** P2. **Design:** [DESIGN.md](DESIGN.md) §7 — thin, needs real design work first.

### Notes
No prior art to absorb — RimTalk Dialogue Patch is a UI mod for RimTalk's talk window despite the
name. RimTalk Custom Events already solves a close problem with JSON-authored multi-phase events;
reuse that format rather than inventing a second one, and consider whether this pillar belongs in
that mod instead of this one.

### Tasks
- [ ] Design it properly in [DESIGN.md](DESIGN.md) §7 before any code
- [ ] Decide: reuse Custom Events' JSON shape, a new format, or move the feature there entirely
- [ ] Trigger conditions and slot filling
- [ ] Authoring UI

### Exit criteria
1. A player-authored template fires on its trigger and reads naturally.
2. It can be authored without editing a file by hand.

---

## P8 — Actions

**Goal:** unknown until the reference lands.

**Depends on:** reference material. **Design:** none yet — the acknowledged gap.

"A better action system with smart action type retrieval" reads two ways, and they are different
projects:

1. **Dialogue drives the game** — what a pawn says produces a RimWorld outcome: a job, an
   interaction, a mood effect, a relationship change.
2. **Better action selection inside RimTalk** — it already classifies responses (`InteractionType`:
   None / Insult / Slight / Chat / Kind); this would choose among them more intelligently, scored
   the way memory retrieval is scored.

An actions mod is being added to `references/`. Read it first, then write the design.

### Tasks
- [ ] Read the reference once it lands
- [ ] Settle which reading this is
- [ ] Write the design in [DESIGN.md](DESIGN.md)
- [ ] Re-scope this pillar and place it in the order above

---

## P9 — Literature and quests

**Goal:** books, art and quests written by the model instead of by RimWorld's template grammar.

**Depends on:** nothing. **Design:** [DESIGN.md](DESIGN.md) §14.

### Why it is independent
These are not conversations. There is no pawn talking and no prompt to inject into, so this uses
`AIClientFactory.GetAIClientAsync()` — the player's already-configured provider, key and model —
and makes its own request. It touches none of P1–P8 and can be pulled forward at any time.

### Tasks
- [ ] Read the prior art: RimTalk – Expand Literature (`cj.rimtalk.literature`, by RimTalk's own
      author) and RimTalk – Quests (`rimtalk.quests`)
- [ ] A small client wrapper in `Source/Integration/` — every borrowed-client call in one place,
      same rule as `RimTalkApi`
- [ ] **Caching and storage first.** A book generated once and stored costs one request; one
      regenerated on inspection costs a request every time someone looks at a shelf. Get this right
      before generating anything.
- [ ] Books: title and description
- [ ] Art: sculpture and engraving descriptions
- [ ] Quests: descriptive of the real quest parameters, **never a source of them** — text that
      contradicts the quest's mechanics is worse than dull text
- [ ] Failure behaviour: no API key, no network, quota exhausted. Vanilla text, quietly.

### Exit criteria
1. Two copies of the same book title read differently and plausibly.
2. A generated quest description matches what the quest actually asks for.
3. Nothing regenerates on inspection; the request count stays flat while reading.
4. With the API unreachable, everything falls back to vanilla text with no errors.

---

## P10 — Mod integration and detection

**Goal:** other mods' content becomes something pawns can talk about, without hard references.

**Depends on:** P2. **Design:** [DESIGN.md](DESIGN.md) §15.

### Tasks
- [ ] Integration profile registry keyed by package id, activated on `ModsConfig.IsActive`
- [ ] Profiles contribute through the same declaration mechanism as everything else, so they
      inherit the budget and appear in the profile panel
- [ ] Reflection only — an integration that crashes when its mod is absent is worse than none
- [ ] **RimTalk Custom Events** (`ethan.rimtalkcustomevents`) — ours, so it can be designed from
      both sides at once
- [ ] **Arkhdottir**
- [ ] Panel view: which integrations are active and what each contributes

### Exit criteria
1. With an integration's mod absent, nothing loads and nothing errors.
2. With it present, its content reaches the prompt and shows in the panel.
3. Custom Events and Memories running together produce no duplicated context.

---

## P11 — RJW compatibility

**Goal:** an optional module for colonies running RJW.

**Depends on:** P3, P10. **Design:** [DESIGN.md](DESIGN.md) §16. **Built last.**

Last for two reasons: it is the only feature with a hard dependency on a mod most players do not
run, so it must be cleanly separable; and it is where context reaching the wrong prompt matters
most, which wants P3's earshot and scoping finished rather than bolted on.

### Tasks
- [ ] Gate entirely on `rim.job.world`, through P10's detection
- [ ] Read the prior art: `RimtalkRJW2` (already installed) and `kuwa.RJWSexInteractionReport`
- [ ] Context contributions, scoped by the earshot rules from P3
- [ ] Verify the module compiles out cleanly — no RJW types in the main assembly's references

### Exit criteria
1. With RJW absent, no errors, no loaded types, nothing in the panel.
2. With RJW present, context reaches only the pawns P3's rules say it should.
