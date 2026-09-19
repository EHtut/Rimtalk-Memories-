# Pillars

The work, cut into pieces that can be finished one at a time.

**This file owns the decomposition** — what each pillar covers, what it deliberately does not,
what it depends on, and how we know it is done. It records no progress: that is
[STATUS.md](STATUS.md). What each feature *is* lives in [DESIGN.md](DESIGN.md).

## How we work through it

One pillar at a time, in order, except where the graph says two are independent. A pillar gets its
own spec in `docs/specs/` when it is started, not before — a spec written three pillars early is a
spec that will be wrong by the time anyone reads it.

A pillar is **done** when its exit criteria are observable in a running game, not when the code
compiles.

**But loading RimWorld costs ~20 minutes**, so there is one verification run per pillar, at the
end. Three rules follow:

- **Build the instrument before running the experiment.** Whatever is needed to read the result
  must exist before the run.
- **Push everything possible to the main menu.** RimWorld loads mod settings at startup, so a panel
  in Mod Options is inspectable with no save loaded.
- **Run `tools/smoke-test.ps1` before every verification run.** It drives the built assembly by
  reflection with no game loaded. It has already caught a `MissingMethodException` that would have
  presented in game as a dead feature, and a startup-coupling bug that made the catalogue
  unreadable headless.

Each pillar ends with a single test checklist, and by updating [STATUS.md](STATUS.md) and, if the
design moved, [DESIGN.md](DESIGN.md). Same session.

## Phases

Going standalone means an engine has to exist before anything speaks. That is Phase A, and it is
the price of the decision in [DESIGN.md](DESIGN.md) §2.

```
A. ENGINE          P1 Core ──► P2 Model client ──► P3 Talk engine ──► P4 Display
                                     │
B. VOICE                             └──► P5 Context & prompt ──► P6 Earshot & delivery
                                                                        │
C. MEMORY                                          P7 Memory core ◄─────┘
                                                        └──► P8 Recall ──► P9 Smart context

D. CONTENT         P10 Templates    P11 Actions    P12 Literature & quests (independent)
E. INTEGRATION     P13 Mod detection    P14 RJW (last)
```

**P12 depends on nothing past P2.** Books and quests are not conversations — they call the model
client directly ([DESIGN.md](DESIGN.md) §14). Once P2 exists, P12 can be pulled forward any time.

**P6 before P7** is deliberate: writing a memory means deciding who witnessed the thing, which is
the earshot question P6 answers.

---

# Phase A — Engine

## P1 — Core

**Goal:** settings, logging, the budget, and a panel that shows what we would send.

**Depends on:** nothing. **Design:** [DESIGN.md](DESIGN.md) §3, §4, §12.

### Tasks
- [x] Settings in `ModSettings`, logging, conflict detection
- [x] Character budget, priority-ordered
- [x] Prompt slots and the section catalogue
- [x] Prompt profile panel, reachable from the main menu
- [x] `tools/smoke-test.ps1` — drives the assembly with no game loaded
- [x] Standalone: no mod dependencies beyond Harmony

### Exit criteria
1. Panel opens from the main menu and shows the slots, every section's text, and the budget split.
2. Mod loads with no errors; the conflict warning fires when RimTalk is present.

## P2 — Model client

**Goal:** talk to a language model, and fail well when we cannot.

**Depends on:** P1. **Design:** [DESIGN.md](DESIGN.md) §17.
**Spec:** [docs/specs/P2-model-client.md](docs/specs/P2-model-client.md)

### Tasks
- [x] `IModelClient` and a message/completion shape
- [x] **Mock provider first** — canned replies, configurable delay and failure rate. Not a test
      fixture: it is what lets P3, P4 and all of Phase C be built and demonstrated with no key, no
      network and no bill.
- [x] OpenAI-compatible provider — covers OpenAI, OpenRouter, DeepSeek, Together, LM Studio and
      Ollama's compatibility endpoint in one implementation
- [x] JSON reader and writer — RimWorld ships no Newtonsoft
- [x] Error handling as a first-class path: no key, bad key, rate limit, quota, timeout, malformed
      response, local server down. Each distinct, quiet and player-readable.
- [x] Settings UI: provider, key, model, endpoint. Key hidden behind a Show toggle, never logged.
- [x] Harness coverage for request shaping, response parsing and every failure branch
- [ ] Gemini native provider
- [ ] Player2 last — its device-auth flow is the only one needing an interactive login
- [ ] **Test-connection button** — otherwise the only way to check a key is to load a colony
- [ ] Token count and running cost surfaced in the profile panel
- [ ] Retry with backoff, transient failures only

### Exit criteria
1. A round trip against the mock provider, entirely headless.
2. A round trip against a real OpenAI-compatible endpoint.
3. Every failure mode produces one clear message and no exception spam.
4. No key configured is a calm, explained state — not an error every tick.

## P3 — Talk engine

**Goal:** the right colonist says something at the right moment.

**Depends on:** P2. **Design:** [DESIGN.md](DESIGN.md) §18.

### Tasks
- [x] Per-pawn state, built lazily; absence is normal, and pruned so dead pawns do not leak
- [x] Eligibility checks, cheapest first, each rejection carrying a reason
- [x] Selection weighted by how overdue a colonist is, scaled by chattiness
- [x] Tick-driven scheduling with a player-set interval and an in-flight cap — every request costs
      money, so this is a spend control as much as a pacing one
- [x] Prompt assembly on the main thread, dispatch off it, results marshalled back
- [x] Response parsing, tolerant of prose, code fences and chat around the JSON
- [x] Assembler walks `PromptCatalog` slots in order and respects the budget
- [x] Core sections: system instruction and output contract, exempt from the budget
- [x] Engine diagnostics in the panel's Live tab, including *why* nothing is happening
- [ ] Real earshot rules in place of the placeholder radius — that is P6's job, not a gap here

### Exit criteria
1. Colonists hold a conversation end to end against the mock provider.
2. Nothing touches game state from a worker thread.
3. The in-flight cap holds under a busy colony.
4. A malformed reply degrades to no line, not an exception.

## P4 — Display

**Goal:** speech appears somewhere worth looking.

**Depends on:** P3. **Design:** [DESIGN.md](DESIGN.md) §19.

### Tasks
- [ ] Overhead bubbles: themable, length-capped, dismissal timer
- [ ] Interaction log entries, so dialogue is scrollable after the fact
- [ ] Defer to Bubbles when it is installed rather than fighting it
- [ ] Log every Harmony patch in [DESIGN.md](DESIGN.md) §10 as it lands

### Exit criteria
1. Dialogue is readable above pawns and in the log.
2. With Bubbles installed, one bubble appears, not two.

---

# Phase B — Voice

## P5 — Context and prompt

**Goal:** the player can author how colonists sound and what they all know.

**Depends on:** P1 (and P3 to see it land). **Design:** [DESIGN.md](DESIGN.md) §4, §5, §6.

### Tasks
- [x] Age voice, five bands, player-editable
- [x] Gender voice, off by default with empty text
- [x] World lore, character-capped, word-boundary trim
- [ ] Colony lore, and the rule keeping it from prisoners and visitors
- [ ] Per-save lore override, so one colony's history is not global
- [ ] The `SystemInstruction` and `OutputContract` slots, written and editable
- [ ] Persona: who a colonist is, beyond their register
- [ ] Settings UI pass once the field count outgrows one scroll pane

### Exit criteria
1. A child, an adult and an elder speak in visibly different registers.
2. World lore appears once per prompt, not once per participant.
3. A prisoner does not speak from colony-internal knowledge.
4. Turning a feature off removes its text from the next prompt, verified in the panel.

## P6 — Earshot and delivery

**Goal:** who can hear what, and the difference between a whisper and a shout.

**Depends on:** P3. **Design:** [DESIGN.md](DESIGN.md) §9.

### Tasks
- [ ] **The earshot model first.** One function: can B perceive A saying X from distance D, through
      this wall, at this volume? Everything else is a caller, and P7 inherits witness detection.
- [ ] Distance settings: talk, hearing, viewing, announcement, context radius, same-room
- [ ] Suppress the "slighted" thought when a pawn was merely out of earshot
- [ ] Response types: whisper, shout, thought — driving prompt and audience
- [ ] Separate length caps for monologue and conversation

### Exit criteria
1. Pawns in separate rooms do not converse with same-room on.
2. A whisper reaches its target and nobody else; a shout carries further.
3. A thought is visible to the player and to no pawn.
4. Being out of earshot does not generate a snub debuff.

---

# Phase C — Memory

## P7 — Memory core

**Goal:** things that happen get remembered, with weight, and survive a save.

**Depends on:** P6. **Design:** [DESIGN.md](DESIGN.md) §8.1–§8.3.

### Tasks
- [ ] Memory record and its `ExposeData`
- [ ] Persistence in a `GameComponent`, including the caravan case
- [ ] Capture sources: conversations, colony events, battle and social log. Decide what is worth a
      memory at all — the difference between a life and a diary of every meal.
- [ ] Witness detection via P6's earshot model
- [ ] Significance at write time; decay with a half-life that scales with it; pinning
- [ ] Bounded per-pawn candidate set — what makes P8 affordable
- [ ] Inspector: one pawn's memories with scores and ages

### Exit criteria
1. A raid, a death and a shared meal rank in that order by significance.
2. Memories survive save, quit and reload, and a caravan round trip.
3. Trivia has faded after a season; a death has not.
4. The candidate set stays bounded in a long colony.

## P8 — Recall

**Goal:** the right memories, at the right moment, arriving together.

**Depends on:** P5, P7. **Design:** [DESIGN.md](DESIGN.md) §8.1, §8.3, §8.4.

### Tasks
- [ ] Relevance scoring against the present moment
- [ ] Retrieval: decayed significance combined with relevance, top N above a floor
- [ ] Memory links, and chained recall at a lower bar with depth and total capped
- [ ] Fill the `Memory` slot
- [ ] Panel view: what was chosen, and the score breakdown that chose it

> The score-breakdown view is not optional polish. Significance, relevance, decay and chain bar are
> four interacting knobs, and tuning four knobs blind does not converge.

### Exit criteria
1. A colonist brings up a relevant past event unprompted, in a fitting moment.
2. They recall a raid and the person they lost in it together, not as two unrelated lines.
3. The same memory does not resurface every conversation.
4. No measurable frame cost when a pawn speaks in a long colony.

## P9 — Smart context

**Goal:** send what matters now, instead of everything every time.

**Depends on:** P8. **Design:** [DESIGN.md](DESIGN.md) §13.

### Tasks
- [ ] Reuse P8's relevance scorer against context fragments — one scorer, not two
- [ ] Per-prompt allocation, cheap enough for the tick path
- [ ] Panel view: what was dropped this prompt, and why

### Exit criteria
1. A colonist arguing about dinner does not carry their medical history into the prompt.
2. One who was just shot does.
3. Measurably fewer characters sent, with no loss of what mattered.

---

# Phase D — Content

## P10 — Dialogue templates

**Depends on:** P5. **Design:** [DESIGN.md](DESIGN.md) §7 — thin, needs real design first.

- [ ] Design it properly before any code
- [ ] Decide: reuse RimTalk Custom Events' JSON shape, a new format, or move the feature there
- [ ] Trigger conditions and slot filling, and an authoring UI

## P11 — Actions

**Depends on:** P3. **Design:** none yet.

"A better action system with smart action type retrieval" reads two ways: dialogue producing
RimWorld outcomes (jobs, interactions, mood, relationships), or smarter classification of what kind
of social act a line is. The reference that settles it is now in `references/Actions`
(`RimTalk-ExpandActions.dll`) and has not been read.

- [ ] Read the reference
- [ ] Settle which reading this is, and write the design
- [ ] Re-scope this pillar

## P12 — Literature and quests

**Depends on:** P2 only. **Design:** [DESIGN.md](DESIGN.md) §14.

Independent of everything in Phases B and C — pull it forward whenever the context work needs a
break.

- [ ] Read the prior art in `references/Rimtalk Expand Lit` and `references/Rimtalk Expand Quests`
- [ ] **Caching and storage first** — a book regenerated on inspection costs a request every time
      someone walks past a shelf
- [ ] Books, then art, then quests
- [ ] Quest text must be descriptive of real quest parameters, never a source of them
- [ ] Falls back to vanilla text, quietly, when the model is unreachable

---

# Phase E — Integration

## P13 — Mod integration and detection

**Depends on:** P5. **Design:** [DESIGN.md](DESIGN.md) §15.

- [ ] Profile registry keyed by package id, activated on `ModsConfig.IsActive`
- [ ] Profiles contribute through the normal section mechanism, inheriting budget and panel
- [ ] Reflection only — an integration that crashes when its mod is absent is worse than none
- [ ] **RimTalk Custom Events** (ours, designable from both sides) and **Arkhdottir**

## P14 — RJW compatibility

**Depends on:** P6, P13. **Design:** [DESIGN.md](DESIGN.md) §16. **Last.**

- [ ] Gate entirely on `rim.job.world` through P13's detection
- [ ] Read `RimtalkRJW2` and `kuwa.RJWSexInteractionReport`
- [ ] Context contributions scoped by P6's earshot rules
- [ ] Verify it compiles out cleanly — no RJW types in the main assembly's references
