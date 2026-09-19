# Arkh — design

What this mod is, how it is put together, and how each feature is meant to work.

This file describes **intent**, and owns it. Three companions, each owning something this one
does not:

- [PILLARS.md](PILLARS.md) — how the work is cut up and in what order. Start there to pick up work.
- [STATUS.md](STATUS.md) — how far along each pillar is. The only place progress is recorded.
- [docs/RIMTALK-TEARDOWN.md](docs/RIMTALK-TEARDOWN.md) — prior art. RimTalk solved many of these
  problems first; what it does and how is worth knowing before solving them again.

---

## 1. The premise

Colonists who talk, and who **remember**.

The memory system is the point. Everything else exists to serve it: context worth remembering,
prompts worth spending tokens on, and rules about who could plausibly have heard a thing. A
transcript of recent lines is not a memory — it forgets a death the moment the conversation moves
on, and recalls what somebody had for lunch with exactly the same fidelity.

## 2. Standalone

Arkh generates dialogue itself. It does not extend another mod, and it has no mod dependencies
beyond Harmony.

### 2.1 Why, and what it costs

This started as a companion to RimTalk. That was reconsidered and reversed, for one reason that
turned out to be decisive: **RimTalk's prompt is a player-owned preset.** Displacing its content
meant either telling players to enable advanced prompt mode and delete three entries by hand, or
silently rewriting their configuration. The first is not an onboarding step anyone will follow;
the second is not ours to do. Everything else — version fragility, the compatibility surface — was
secondary to that.

**The cost is real and worth stating plainly.** Provider plumbing, async orchestration, pawn
selection, response parsing and speech display all have to exist before a single colonist says
anything. That is §17, §18 and §19, and they are the price of the decision.

Two things soften it. A **mock provider** means the whole pipeline can run with no API key and no
network. And a **headless harness** (`tools/smoke-test.ps1`) drives the built assembly by
reflection with no game loaded — which matters enormously when loading RimWorld costs twenty
minutes. Neither is optional; both are what make the engine work testable at all.

### 2.2 What survived the change

Roughly two thirds of the earlier work carried over unchanged, because it was never about RimTalk:
the character budget and its priority order, settings, logging, the age / gender / lore text
producers, the profile panel, and the harness. What died was the seam and the anchor plumbing.

### 2.3 Prior art, not source

`references/` holds RimTalk and several of its companions, decompiled, for **reading**. That
folder is gitignored, and stays that way: those mods ship no licence, so redistributing them is
not ours to do. We reimplement against vendors' published HTTP APIs and our own design, and we
credit the prior art. See [references/README.md](references/README.md).

## 3. Lifecycle: where state lives, and when things happen

### 3.1 Settings live in mod settings, never in the save

**Every player-facing setting Arkh owns is a `ModSettings` field**, saved to RimWorld's config
folder — not scribed into the save game. `ArkhSettings` is the one place they live.

Three consequences, and the third is the one that pays:

1. **They carry across colonies.** Authored text — voices, lore, prompt wording, API keys — is work
   the player did once.
2. **They are not colony history.** Anything describing a *particular* colony's past belongs in the
   save via a `GameComponent`. Memories (§8) are the obvious case. The line is: authored by the
   player → settings; produced by play → save.
3. **They are readable at the main menu**, because RimWorld loads mod settings at startup.

That third point is what lets the prompt profile panel work with no save loaded, and at twenty
minutes a load that is the difference between a usable tool and a useless one. It is a constraint
on future features as much as a convenience: **if a thing can be inspected from settings alone, it
should be.**

### 3.2 Catalogues must not need a running game

`PromptCatalog` holds declarations and has no `StaticConstructorOnStartup`. Startup work — the
conflict warning, the ready log line — lives in `ArkhStartup` instead.

That separation is not tidiness. A type that both holds data and does startup work cannot be read
outside a running game, which puts it out of reach of the harness — and then the harness ends up
testing a hand-written mirror of the real declarations, which drifts. Keep data types inert.

## 4. The prompt we build

Ours, end to end. Seven slots, emitted in order:

| Slot | Built | Holds |
|---|---|---|
| `SystemInstruction` | once | who the model is, how to behave |
| `OutputContract` | once | the response format we parse |
| `WorldContext` | once | true of the place — lore, weather, events |
| `PawnContext` | per participant | true of the person — who they are, how they speak |
| `Memory` | per participant | what they remember, retrieved and scored |
| `Conversation` | once | recent lines, for continuity within an exchange |
| `Trigger` | once | what is prompting this line right now |

**Memory is a slot.** That is the difference that motivated the whole project: what a pawn
remembers is part of who is speaking, not a transcript appended to the end.

Sections declare which slot they belong to and their order within it. Nothing anchors to anything
else's categories; the slot *is* the position. `OutputContract` is separated from
`SystemInstruction` on purpose — its wording is a contract with our own parser rather than a matter
of taste, so it should be obvious when someone is editing it.

## 5. Age and gender voice

The prompt carries a pawn's age already, but a number alone does little: handed "age 6", a model
still writes a six-year-old who argues like a lawyer. What changes the output is an instruction
about **register** — sentence length, what they grasp, what they care about.

Five bands: baby, child, teenager, adult, elder, cut on **biological** years so a pawn out of
cryptosleep sounds like the body doing the talking.

Adults get an empty string by default, and empty sections are skipped. An ordinary adult is the
model's default register already, so spending tokens to say so is waste on every prompt in the
colony.

Gender voice works the same way but ships **off, with empty defaults**. The model is told each
pawn's gender regardless; anything added here is the player asserting that men and women should
*sound* different. Some campaigns want that and many do not, so it stays dark until someone fills
the boxes in.

Non-humanlike pawns get no band. The thresholds are human ones, and applying them to an animal
would label a two-year-old boomalope a toddler.

## 6. World lore

Free text every pawn on the map speaks from: what this world is, who runs it, what is taken for
granted here.

**World** context, not pawn context — it is true of the place, not the person, so it is built once
per prompt rather than once per participant. For a four-pawn conversation that is the difference
between paying for the lore once and paying four times.

Character-capped and trimmed at a word boundary, because it rides on every prompt the colony
generates.

Colony-scoped lore is a separate future block: it must be withheld from outsiders and prisoners,
which world lore need not be.

## 7. Dialogue templates

Not yet designed in detail. Authored beats the player wants to happen on purpose, with trigger
conditions and slots the current game state fills in.

RimTalk Custom Events already solves a close problem with JSON-authored multi-phase events. Reuse
that format rather than inventing a second one, and consider whether this belongs in that mod.

## 8. Memory

The namesake feature, and the one with real design in it.

### 8.1 Two scores, computed at different times

**Significance** — how much this mattered, *ever*. Assigned once, when the memory is written, then
stored. Derived from the event's kind (a bonded animal dying outranks a good meal), scaled by mood
swing at the time and by closeness to whoever else was involved.

**Relevance** — how much it matters *now*. Computed at recall against the current situation:
overlap between the memory's participants and who is present, proximity to where it happened,
topical match against the pawn's job, thoughts and active events, and whether its emotional colour
matches their current mood.

Keeping these apart is what makes retrieval work. Significance alone means a pawn recites the worst
day of their life over breakfast forever. Relevance alone means nothing has weight.

### 8.2 Decay

Significance decays with age, but the half-life scales *with* significance, so trivia fades in days
and a death does not. Pinned memories do not decay.

### 8.3 Retrieval, and the cost ceiling

Final score combines decayed significance and current relevance; take the top N above a floor.

Scoring every memory a pawn owns, every time they open their mouth, is not affordable. So:
significance is precomputed at write time; each pawn keeps a bounded candidate set in
decayed-significance order; relevance is only computed for that bounded set. Retrieval is a cheap
scan over tens of entries, not a search over a life.

### 8.4 Chained recall

Memories hold links to each other — same originating event, shared participants, causal follow-on.
When a memory is selected its links come too, at a **lower** score bar: the chain exists to supply
context the model needs to make sense of the first memory, so it should not have to independently
earn its place.

Depth and total are capped, or one well-connected event drags in the entire war.

The intended effect: a pawn recalls the raid *and* the person they lost in it in one breath,
instead of two disconnected lines.

## 9. Earshot, lengths, and response types

**Earshot.** One model answering: can B perceive A saying X, from distance D, through this wall, at
this volume? Everything else is a caller — conversation eligibility, witness detection for memory,
and the audience rules for whispers.

Since we own pawn selection (§18), this is ours to define rather than something to patch into.
RimTalk Distance Control's numbers are a sane starting point: talk 20, hearing 10, viewing 20,
announcement 30, same-room on.

**Lengths.** Separate caps for monologues and for conversations — one long monologue reads very
differently from four long turns of dialogue.

**Response types.** Whisper, shout and thought as distinct kinds of speech, driving both the prompt
(how it should be phrased) and the display (who can see it). A thought has an audience of one.

## 10. Where Harmony is needed

Arkh owns its own pipeline, so Harmony is only for reaching into **RimWorld**, never into another
mod. Kept here and only here, so the surface is always countable.

| Need | Why | Status |
|---|---|---|
| Suppress the "slighted" thought when a pawn was merely out of earshot | `MemoryThoughtHandler.TryGainMemory` | §9, not built |
| Observe battle and social log entries as memory sources | `BattleLog.Add`, `PlayLog` | §8, not built |

Every patch added must be listed here as it lands, with its reason.

**Phase A shipped with none.** The whole engine — client, selection, scheduling, threading, parsing,
display — needed no Harmony at all. Owning the pipeline makes scheduling and selection ours by
construction, and display goes through vanilla `PlayLog` because that is where Interaction Bubbles
already listens (§19). Worth protecting: every patch avoided is a way RimWorld updates cannot break
this mod.

## 11. Open questions

- **Colony lore scoping.** Prisoners and visitors must not speak from colony-internal knowledge.
  Where does the eligibility check live — memory, or context?
- **Memory persistence across colonies.** Per-save (`GameComponent`), but a pawn who leaves in a
  caravan and returns must keep theirs.
- **Streaming or not.** Streaming shows text sooner; non-streaming is far simpler to parse and
  retry. Start non-streaming unless the latency is intolerable.

## 12. The character budget

One place knows how much prompt text Arkh may produce, and divides it. Without that, every feature
caps itself in isolation and eight "reasonable" caps still produce a prompt nobody intended.

Allocation is **priority-ordered and greedy**. The order is a design statement, not a tuning
constant, and lives in `BudgetOrder`:

1. **What a pawn remembers** — usually the reason the line was worth generating.
2. **How they speak** — age, then gender. Short, and they change the output out of all proportion
   to their length.
3. **What everyone knows** — colony lore, then world lore. Background, large, not specific to this
   moment, so they are what should give way.

Pawn sections are built once per participant and world sections once per prompt, so a pawn
section's real cost is its text times the number of people talking. The budget multiplies
accordingly, using a player-set assumed conversation size.

Counting is in **characters, not tokens** — real tokenisation is not affordable on the tick path
and varies by provider. Roughly four characters per token in English; CJK runs far denser, so it is
presented to the player as what it is, a character budget, rather than a token estimate dressed up
as precision.

**Some sections are exempt.** The system instruction and the output contract are taken off the top
in full, before anything competes for the rest, and they may push the total over its ceiling. That
is the intended trade: a trimmed output contract makes the model answer in a shape the parser
cannot read, losing *every* reply, where an overspend of a few hundred characters loses only a
little money. Being merely first in the priority order is not enough — a small enough budget would
still trim them. Exemptions are text the player cannot budget away, so they stay rare.

Recomputed only when something changes, never per prompt. §13 is where that stops being enough.

## 13. Smart context

Sending the same context every time is thorough and mostly wasted — a pawn arguing about dinner
does not need their full medical history, and those tokens are tokens not spent on something that
mattered.

**Smart context scores each fragment against the present moment** and spends the budget on what
scores highest. It is the same machinery as memory relevance (§8.1) pointed at context instead of
memories, and the two should share an implementation rather than growing two scorers.

This turns §12's allocation from static into per-prompt, which is the one thing §12 says it does
not do — so allocation has to become cheap enough to run per prompt: bounded candidate sets and
precomputed scores, exactly as §8.3 requires.

## 14. Literature and quests

Books, art and quests written by the model rather than by RimWorld's template grammar.

These are **not conversations**: no pawn is talking, and none of the context pipeline applies. They
call the model client (§17) directly with their own messages, which makes this work largely
independent of §4–§13.

Three targets, increasing in difficulty:

- **Books.** Title and description, so a colony library is not six copies of one generated tract.
- **Art.** Sculpture and engraving descriptions, which RimWorld generates from a grammar and which
  read as such.
- **Quests.** Harder, because quest text is load-bearing. Generated text must be *descriptive of*
  the real quest parameters and never a source of them — a description contradicting the quest's
  mechanics is worse than a dull one.

Caching is the whole game. A book generated once and stored costs one request; one regenerated on
inspection costs a request every time someone walks past a shelf.

## 15. Mod integration and detection

Other mods add things pawns should be able to talk about. A detection layer lets a mod's presence
contribute vocabulary, context and events without Arkh hard-referencing it.

A registry of integration profiles keyed by package id, activated only when `ModsConfig.IsActive`
says so, contributing through the same section mechanism as everything else so they inherit the
budget and appear in the profile panel. Reflection only: an integration that crashes when its mod
is absent is worse than no integration.

First targets: **RimTalk Custom Events** (`ethan.rimtalkcustomevents`, ours — so it can be designed
from both sides) and **Arkhdottir**.

## 16. RJW compatibility

Optional module, gated on `rim.job.world`, built **last**. It is the only feature with a hard
dependency on a mod most players do not run, so it must be cleanly separable; and it is where
context reaching the wrong prompt matters most, which wants §9's earshot rules finished first.

## 17. The model client

The layer RimTalk used to provide. One interface, several providers, written against each vendor's
published HTTP API.

```
IModelClient
    Task<Completion> Complete(IReadOnlyList<Message> messages, CancellationToken ct)
```

**Providers.** OpenAI-compatible first — one implementation covers OpenAI, OpenRouter, DeepSeek,
Together, LM Studio and Ollama's compatibility endpoint. Then Gemini's native shape. Player2's
device-auth flow last, because it is the only one needing an interactive login.

**A mock provider is a first-class provider, not a test fixture.** It returns canned lines with a
configurable delay and failure rate. It is what lets the talk engine, the display layer and the
memory system be developed and demonstrated without an API key, a network, or a bill — and what
lets the harness exercise the whole pipeline headless.

**Failure is the common case, not the exception**: no key, bad key, rate limit, quota exhausted,
timeout, malformed response, local server down. Each needs a distinct, quiet, player-readable
outcome. A colonist silently not speaking is the worst of them, because it looks like a bug in
everything else.

Keys are `ModSettings` (§3.1), so they carry across colonies — and are never written to the log.

## 18. The talk engine

Who speaks, when, and what happens to the reply.

- **Pawn state.** A per-pawn record: eligibility, last spoken tick, chattiness weight, pending
  requests. Built lazily; absence is normal, not an error.
- **Eligibility.** Awake, not drafted, spawned, on a map the player is looking at, not already
  generating. Cheap checks first.
- **Selection.** Weighted by chattiness and time since last spoken, gated by §9's earshot rules.
- **Scheduling.** A tick-driven loop with a player-set interval and an in-flight cap, because every
  request costs money.
- **Async.** Assemble the prompt on the main thread, dispatch off it, marshal the result back.
  Nothing touches game state from a worker thread.
- **Parsing.** Structured output against the §4 `OutputContract`, tolerant of the model padding it
  with prose or code fences.

## 19. Display

**Interaction Bubbles is a hard dependency, and drawing is its job.**

Bubbles already solves overhead speech well, is widely installed, and has settings players have
tuned to taste — font, duration, opacity, hearing range. Writing a second implementation would mean
a second set of controls doing the same job, and two bubbles per line for anyone running both.
Depending on it is a smaller, better mod than competing with it.

That makes this the smallest piece of the engine. Bubbles postfix-patches vanilla `PlayLog.Add`,
accepts any `PlayLogEntry_Interaction`, and renders that entry's text above the initiator. So the
entire bridge is: put a `PlayLogEntry_ArkhSpeech` into the play log.

The consequences are all in our favour:

- **No assembly reference to Bubbles, and no patch of our own.** We touch vanilla only, so a
  Bubbles update cannot break us.
- **The social log comes free.** The same entry is a real log entry, so dialogue is scrollable
  after the fact without a second code path.
- **Its hearing rules apply automatically.** Bubbles already declines to draw for a pawn nobody
  could hear.

The one thing we own is the text: `ToGameStringFromPOV_Worker` returns our line rather than a
rulepack's. Note that is the *protected worker* — the public wrapper is not virtual.

Deliberately not varied by point of view. Vanilla shows a different string to a pawn who could not
hear it, and that distinction is real — but it belongs to the earshot model (§9), which decides who
is a participant at all. Deciding it in two places is how the two come to disagree.

Response types (§9) will need more than this: a whisper should reach one pawn and a thought none.
That is P6's work, and it will shape who the entry names rather than how it is drawn.
