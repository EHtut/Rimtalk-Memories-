# RimTalk Memories — design

What this mod is, how it hooks into RimTalk, and how each feature is meant to work.

This file describes **intent**, and owns it. Three companions, each owning something this one
does not:

- [PILLARS.md](PILLARS.md) — how the work is cut up and in what order. Start there to pick up work.
- [STATUS.md](STATUS.md) — how far along each pillar is. The only place progress is recorded.
- [docs/RIMTALK-API.md](docs/RIMTALK-API.md) — the RimTalk API facts these designs rest on. The
  only place they are written down.

---

## 1. The premise

A RimTalk player who wants a richer colony currently installs five or six companion mods, each
by a different author, each patching RimTalk's internals independently. They overlap, they
duplicate context, and they break on different schedules.

RimTalk Memories is one mod in place of that shelf. The point is not only fewer entries in the
mod list — it is that features which *should* talk to each other finally can. Memory retrieval
can read the distance rules to decide who could plausibly have witnessed a thing. Response type
can read significance to decide whether something is muttered or shouted. Separate mods cannot
do that; one mod can.

## 2. Build on the API, not on Harmony

RimTalk ships a real extension API — `RimTalk.API.RimTalkPromptAPI` — that covers context
injection, template variables and prompt-preset editing. Most of the mods being replaced here do
not use it. Context Upgrade patches fourteen private RimTalk methods with Harmony, including
`PromptService.BuildContext`, `PromptManager.BuildMessages` and `ContextBuilder`. That is why
those mods break whenever RimTalk moves.

**Rule for this project: if the public API reaches it, use the public API.** Harmony is a last
resort, each use is justified in §10, and each one is isolated behind
`Source/Integration/` so the blast radius of a RimTalk update stays small.

The single seam is `Source/Integration/RimTalkApi.cs`. Feature code never calls RimTalk directly.

### 2.1 Four ways to reach the prompt, and they are all dynamic

It is easy to look at the anchor list and conclude we are limited to decorating 33 pawn fields.
We are not. There are four mechanisms, and the last two are the powerful ones:

| | Mechanism | Placement | Content |
|---|---|---|---|
| 1 | **Hook** on a category | Folded into that category's value | Computed per prompt |
| 2 | **Injection** at a category | A block beside that category | Computed per prompt |
| 3 | **Prompt entry + registered variable** | *Anywhere in the message list* | Computed per prompt |
| 4 | **Borrowed AI client** | Not a prompt at all — our own request | Entirely ours |

**Every preset entry's content is rendered through Scriban at build time**
(`BuildMessagesFromPreset` → `ScribanParser.Render(content, context)`), and Scriban resolution
consults the hook registry for variables other mods registered. So a prompt entry whose content is
`{{rtm_recalled_memory}}`, inserted before or after any named entry, at any role, is a block we
place ourselves and fill freshly on every single prompt. `RegisterContextVariable` hands the
provider the whole `PromptContext` — current pawn, all participants, talk type, chat history, map —
not merely a `Pawn`.

That is arbitrary position with arbitrary per-prompt content. Nothing about attaching to RimTalk
makes prompt injection static.

Mechanism 4 matters for work that is not a conversation at all. `AIClientFactory.GetAIClientAsync()`
is public and returns an `IAIClient` bound to whatever provider, key and model the player already
configured in RimTalk. Rewriting a book or drafting a quest is a one-off generation, not a pawn
talking, so it borrows that connection and skips the prompt pipeline entirely.

**The rule that follows: every setting this mod adds must have a visible path to the prompt.**
A setting that changes nothing the model sees is a lie in the options menu. The injection profile
panel is how that stays honest — if a setting cannot be traced to text in that panel, it is not
finished.

### 2.1a RimTalk as transport: how much of its content is ours?

The goal is RimTalk for what it is good at — provider plumbing, response parsing, pawn selection,
speech display — and *nothing* of what it decides to say. That turns out to be almost entirely
achievable, because RimTalk's whole prompt is **five preset entries**:

| # | Entry | Content | Ours? |
|---|---|---|---|
| 1 | Base Instruction | `Constant.DefaultInstruction` | **Yes** — replace it |
| 2 | JSON Format | the JSONL output contract | **No — load-bearing** |
| 3 | Context *(aka "Pawn Profiles")* | `{{context}}` | **Yes** — remove or filter |
| 4 | Chat History | `{{chat.history}}` | **Yes** — this is the memory to replace |
| 5 | Dialogue Prompt | `{{prompt}}` | Keep — it is the actual trigger |

Entry 3 is the context dump. Entry 4 is RimTalk's "memory", which is nothing more than recent chat
lines. Both are ordinary entries in a mutable `List<PromptEntry>`, and `PromptManager` exposes
`CreateNewPreset`, `AddPreset`, `SetActivePreset` and `RemovePreset` publicly. **We can ship and
activate our own preset.**

Persona is separate and easier: `Pawn:personality` is a context category, so an `Override` hook
replaces it without touching the preset at all.

**Entry 2 must survive in substance.** RimTalk parses JSONL with `name` and `text` keys, plus
optional `act`/`target`. Change that contract and response parsing breaks — which would look like
the model failing rather than like our bug. Rewrite the wording if useful; keep the schema.

#### The catch: advanced prompt mode

`UseAdvancedPromptMode` defaults to **off**, and the two modes differ in a way that decides this
whole approach:

```csharp
PromptPreset preset = rimTalkSettings.UseAdvancedPromptMode
    ? promptPreset
    : PromptPresetAssembler.BuildSimpleModePreset(promptPreset, ...);
```

- **Advanced mode** uses the active preset verbatim. Removing entry 3 or 4 works.
- **Simple mode** rebuilds the preset: built-in entries are normalised and any that are missing are
  **re-inserted**. Removing entry 3 or 4 does not stick.

The saving grace is one branch in that rebuild: entries carrying a `SourceModId` are copied through
untouched. **So our additions survive in both modes; only our removals need advanced mode.**

That sets the design: additive features work for everyone; anything that displaces RimTalk's own
content requires advanced mode. Detect it, explain it, offer to turn it on — do not silently flip
another mod's setting, and do not silently half-work.

#### One cost worth knowing

`talkRequest.Context = PromptService.BuildContext(pawns, ...)` runs **unconditionally**, before the
preset is assembled. Removing entry 3 stops the dump reaching the model but does not stop it being
built, so the work is still paid for on the tick path every prompt.

That is an argument for §13's approach — filter the context rather than discard it, and get value
for the cost already incurred. If we ever do want it gone outright, short-circuiting `BuildContext`
is a legitimate §10 Harmony entry.

### 2.2 Do we fork RimTalk?

Considered seriously, and the answer is no — but the option is kept open, which is why the seam
exists at all.

**What forking would buy:** total control over pawn selection, response post-processing, request
shape. Real limitations, and §10 lists them.

**What it would cost.** RimTalk is roughly 25,000 lines decompiled and actively maintained. It
carries compatibility work for Bubbles, Character Editor, battle logs, ideology, genes and the
interaction log; eight languages; and six LLM providers with streaming, retry, quota handling and
device auth. Reimplementing that is not a pillar, it is a different project. And the premise of
this mod is that companion mods break because they are welded to RimTalk's internals — forking is
the most extreme form of that problem, not the cure for it.

**And the part that settles it:** RimTalk ships no source and no licence file. Forking a
decompiled Workshop mod into a public repository would be redistributing someone else's
copyrighted work without permission. Reading it to understand the API is fine and is why
`references/` exists — and why `references/` is gitignored.

**So:** build on the API, keep every RimTalk call behind `RimTalkApi`, and use Harmony only where
§10 says. If RimTalk ever becomes a genuine blocker, the seam is the thing that makes a different
backend a rewrite of one file rather than of the mod. Revisit this decision if that happens; do
not drift into it.

## 3. Lifecycle: where state lives, and when we register

### 3.1 Settings live in mod settings, never in the save

**Every player-facing setting this mod owns is a `ModSettings` field**, saved to RimWorld's config
folder — not scribed into the save game. `MemoriesSettings` is the one place they live.

Three consequences, and the third is the one that pays:

1. **They carry across colonies.** Authored text — age voices, world lore, prompt wording — is
   work the player did once. Tying it to a save would mean redoing it every new colony.
2. **They are not colony history.** Anything that describes a *particular* colony's past belongs
   in the save instead, via a `GameComponent`. Memories (§8) are the obvious case. The line is:
   authored by the player → settings; produced by play → save.
3. **They are readable at the main menu.** RimWorld loads mod settings at startup, long before any
   save. So is RimTalk's — `PromptManager.SetInstance` is called from `RimTalkSettings.ExposeData`,
   which means RimTalk's active preset and its templates are available there too.

That third point is what makes the injection profile panel work without loading a save, and with a
~20 minute load time that is the difference between a usable tool and a useless one. It is a
constraint on future features as much as a convenience: **if a thing can be inspected from settings
alone, it should be.**

### 3.2 Two registration moments

There are two different times this mod can register things, and mixing them up is the most
likely way to get a silent failure.

| What | Lands in | When to register | Needs a save loaded? |
|---|---|---|---|
| Context sections, hooks, template variables | A static registry | Startup (`StaticConstructorOnStartup`) | No |
| Prompt **entries** (blocks of the preset) | The *active preset* | Game load | Yes |

`RimTalkPromptAPI.AddPromptEntry` and friends resolve `PromptManager.Instance.GetActivePreset()`,
which does not exist before a game is loaded — the call logs a warning and returns `false`.
So preset editing belongs in a `GameComponent`, never in the startup path.

`ContextRegistrar` owns the first column. The second belongs in a `GameComponent`, and the first
feature that needs one is prompt presets (§4) — see [PILLARS.md](PILLARS.md) P2.

## 4. Prompt presets and settings

RimTalk's presets are lists of `PromptEntry` blocks, each with a role (System/User/Assistant), a
position (Relative, or InChat at a given depth), and Scriban template content. Players can
already edit these in RimTalk's own UI.

What this mod adds is **shipped presets worth starting from**, plus the entries that the rest of
the features need in order to say anything. Entries carry `SourceModId`, so
`RemovePromptEntriesByModId` can cleanly take back everything this mod added when a feature is
switched off.

One sharp edge: `SourceModId` on prompt entries is compared **raw**, while hooks and variables
have their mod id lowercased and stripped of punctuation first. Use the same already-clean string
for both — `RimTalkApi.ModId` — or removal silently matches nothing.

## 5. Age and gender voice

RimTalk already puts a pawn's age and gender in the prompt. A number alone does little: handed
"age 6", a model still writes a six-year-old who argues like a lawyer. What changes the output is
an instruction about **register** — sentence length, what they grasp, what they care about.

So this attaches guidance at `ContextCategories.Pawn.Age`. Five bands: baby, child, teenager,
adult, elder, cut on **biological** years so a pawn out of cryptosleep sounds like the body doing
the talking.

**It folds into RimTalk's own age value rather than sitting beside it** (`HookAppend`). Adding a
separate block would state the age twice — once as RimTalk's bare fact, once as ours. Folding also
reaches player-authored templates that reference `{{pawn.age}}`, which an injection never does
(docs/RIMTALK-API.md §1.1).

The attachment mode is a per-section **setting**, not a compile-time choice: injected before or
after, folded in, or replacing RimTalk's text outright. That is a concession to a twenty-minute
load — one session can compare all four instead of one. Replacing is the one to be careful with,
because it means owning that text forever and silently losing RimTalk's improvements to it. The
injection profile panel shows each section's current mode so the choice is never invisible.

Adults get an empty string by default, and empty sections are skipped entirely. An ordinary adult
is the model's default register already, so spending tokens to say so would be waste on every
prompt in the colony.

Gender voice works the same way but ships **off, with empty defaults**. RimTalk already states
each pawn's gender; anything added here is the player asserting that men and women should *sound*
different. That is a choice some campaigns want and many do not, so it stays dark until someone
fills the boxes in.

Non-humanlike pawns get no band. The thresholds are human ones, and applying them to an animal
with a vocal link would label a two-year-old boomalope a toddler.

## 6. World lore

Free text every pawn on the map speaks from: what this world is, who runs it, what is taken for
granted here.

This is **environment** context, not pawn context — it is true of the place, not the person.
That also means RimTalk builds it once per prompt rather than once per participant: for a
four-pawn conversation, the difference between paying for the lore once and paying four times.

Unlike age and gender it is a block in its own right rather than a modification of an existing
value, so it injects (before `Environment:time`) rather than folding. It also registers as
`{{worldlore}}`, so a player who wants it somewhere specific can place it by hand instead of
accepting where the anchor happens to sit.

It is character-capped and trimmed at a word boundary, because this text rides on every prompt
the colony generates.

Colony-scoped lore (what *this* colony knows, as opposed to what the world knows) is a separate
future block: it has to be withheld from outsiders and prisoners, which world lore does not.

## 7. Dialogue templates

Not yet designed in detail. Note that RimTalk Dialogue Patch — despite the name — is a UI mod for
RimTalk's player-facing talk window, **not** a template library. There is no prior art here to
absorb; this is new work.

The shape it should take: authored beats the player wants to happen on purpose, with trigger
conditions and slots the current game state fills in. Close to what RimTalk Custom Events already
does with its JSON events, and that format is worth reusing rather than inventing a second one.

## 8. Memory

The namesake feature, and the one with real design in it.

**What it replaces.** RimTalk's "memory" is one preset entry, `{{chat.history}}` — the last few
things that were said, in order, with no notion of weight or relevance. That is a transcript, not a
memory: it forgets a death the moment the conversation moves on, and it remembers what somebody had
for lunch with exactly the same fidelity.

So this is not an addition beside RimTalk's memory. It is a **replacement for entry 4** (§2.1a):
disable the chat-history entry and substitute one filled by the retrieval below. That needs advanced
prompt mode, for the reason §2.1a gives, and it is the clearest example of why that mode matters.

### 8.1 Two scores, computed at different times

**Significance** — how much this mattered, *ever*. Assigned once, when the memory is written, and
then stored. Derived from the event's kind (a bonded animal dying outranks a good meal), scaled by
mood swing at the time and by closeness to whoever else was involved.

**Relevance** — how much it matters *now*. Computed at recall time against the current situation:
overlap between the memory's participants and who is present, proximity to where it happened,
topical match against the pawn's current job, thoughts and active events, and whether its
emotional colour matches the pawn's current mood.

Keeping these apart is what makes retrieval work. Significance alone means a pawn recites the
worst day of their life over breakfast forever. Relevance alone means nothing has weight.

### 8.2 Decay

Significance decays with age, but the half-life scales *with* significance, so trivia fades in
days and a death does not. Pinned memories do not decay at all.

### 8.3 Retrieval, and the cost ceiling

Final score combines decayed significance and current relevance; take the top N above a floor.

The hard constraint: **context providers run on the main thread, inline, while the tick is
happening** (see [docs/RIMTALK-API.md](docs/RIMTALK-API.md) §4). Scoring every memory a pawn owns,
every time they open their mouth, is not affordable.

So: significance is precomputed at write time; each pawn keeps a bounded candidate set kept in
decayed-significance order; relevance is only ever computed for that bounded set. Retrieval is a
cheap scan over tens of entries, not a search over the pawn's whole life.

### 8.4 Chained recall

Memories hold links to each other — same originating event, shared participants, or a causal
follow-on. When a memory is selected, its links are pulled in too, at a **lower** score bar than
primary selection: the chain exists to supply context the model needs to make sense of the first
memory, so it should not have to independently earn its place.

Chain depth and total pulled are both capped, otherwise one well-connected event drags in the
entire war.

The intended effect: a pawn recalls the raid *and* the person they lost in it in one breath,
instead of two disconnected lines.

## 9. Distance, lengths, and response types

**Distance.** Who can hear what, and how far. Distance Control's numbers are a sane starting
point — talk distance 20, hearing 10, viewing 20, announcement 30, same-room required by default.
This is Harmony territory (§10): RimTalk's pawn selection is not exposed.

**Lengths.** Separate caps for monologues and for conversations, since one long monologue reads
very differently from four long turns of dialogue.

**Response types.** Whisper, shout and thought as distinct kinds of speech. RimTalk's `TalkType`
enum is fixed and not extensible, so these are represented as this mod's own concept layered on
top, driving both the prompt (how it should be phrased) and the display (who can see it).
Whisper and thought need an audience rule — a thought has an audience of exactly one — which is
the same earshot machinery the distance work builds.

## 10. Where Harmony is unavoidable

Kept here, and only here, so the RimTalk-update blast radius is always countable.

| Need | Why the API cannot do it | Owned by |
|---|---|---|
| Conversation distance / who is selected | `PawnSelector` is internal; no API for pawn eligibility | [PILLARS.md](PILLARS.md) P3 |
| Response-type display (whisper/shout/thought) | Speech bubble drawing is internal | P3 |
| Monologue / conversation length caps | No API for response shaping | P3 |
| Skipping RimTalk's context build entirely | `PromptService.BuildContext` runs unconditionally, so dropping the entry stops it being *sent* but not *built* (§2.1a) | Candidate only — §13 prefers filtering it |

Every patch added must be listed here as it lands, with its reason. The context layer (P1, P2)
requires none at all, which is the point of §2.

## 11. Open questions

- **Colony lore scoping.** Prisoners and visitors must not speak from colony-internal knowledge.
  Where does the eligibility check live — memory, or context?
- **Memory persistence across colonies.** Almost certainly per-save (`GameComponent`), but a
  pawn who leaves in a caravan and returns must keep theirs.
- **Does folded age text read well inside a template?** Age now folds into RimTalk's own age value,
  so a template using `{{pawn.age}}` gets `34 — speaks from long experience…` inline. Whether that
  reads naturally is a judgement that needs to be seen, not reasoned about.

## 12. The character budget

One place knows how much prompt text this mod may add, and divides it. Without that, every feature
caps itself in isolation and eight "reasonable" caps still produce a prompt nobody intended.

Allocation is **priority-ordered and greedy**: the most important section takes what it wants, the
next takes what is left. The order is a design statement, not a tuning constant, and it lives in
`BudgetOrder`:

1. **What a pawn remembers** — usually the reason the line was worth generating.
2. **How they speak** — age, then gender. Short, and they change the output out of all proportion
   to their length.
3. **What everyone knows** — colony lore, then world lore. Background, large, and not specific to
   this moment, so they are what should give way.

Pawn sections are built once per participant and environment sections once per prompt, so a pawn
section's real cost is its text times the number of people talking. The budget multiplies
accordingly, using a player-set assumed conversation size.

Counting is in **characters, not tokens** — real tokenisation is not affordable on the tick path
and varies by provider. Roughly four characters per token in English; CJK runs far denser, so the
number is presented to the player as what it is, a character budget, rather than a token estimate
dressed up as precision.

Recomputed only when something changes, never per prompt. §13 is where that stops being enough.

## 13. Smart context

RimTalk builds the same context every time: traits, skills, health, thoughts, social, surroundings.
Thorough, and mostly wasted — a pawn arguing about dinner does not need their full medical history,
and the tokens spent on it are tokens not spent on something that mattered.

**Smart context scores each fragment against the present moment** — the pawn's job, mood,
participants, recent events — and spends the budget on what scores highest, rather than on a fixed
list. It is the same machinery as memory relevance (§8.1) pointed at RimTalk's own context instead
of at memories, and the two should share an implementation rather than growing two scorers.

The mechanism is already available and does not need Harmony: an `Override` hook on a category
replaces RimTalk's value for it, so we can hand back a *filtered* thought list where RimTalk would
have dumped all of them. Context Upgrade does this by patching `ContextBuilder` internals; we can
do it through the published API.

This turns §12's allocation from static into per-prompt, which is the one thing §12 says it does
not do. That is the real cost of this pillar: allocation moves onto the tick path, so it has to
become cheap enough to run per prompt — bounded candidate sets and precomputed scores, exactly as
§8.3 requires for memory.

## 14. Generated literature and quests

Books, art and quests written by the model rather than by RimWorld's template grammar.

These are **not conversations**, and that changes the architecture: there is no pawn talking, no
prompt to inject into, and nothing for the context pipeline to do. They use mechanism 4 from §2.1
— `AIClientFactory.GetAIClientAsync()` returns a client bound to the player's already-configured
provider, key and model, and we make our own request with our own messages.

That means this work is largely independent of everything in §1–§13, and can be built without
touching the context layer at all.

Three targets, in increasing difficulty:

- **Books.** Rewrite title and description so a colony library is not six copies of the same
  generated tract. Cheap: generate once, store on the thing, never regenerate.
- **Art.** Sculpture and engraving descriptions, which RimWorld already generates from a grammar
  and which read as such.
- **Quests.** Harder, because quest text is load-bearing — a description that contradicts the
  quest's actual mechanics is worse than a dull one. Generated text must be *descriptive of* the
  real quest parameters, never a source of them.

Prior art to read first: RimTalk – Expand Literature (`cj.rimtalk.literature`, by RimTalk's own
author) and RimTalk – Quests (`rimtalk.quests`).

Caching is the whole game here. A book generated once and stored costs one request; a book
regenerated on every inspection costs a request every time someone looks at a shelf.

## 15. Mod integration and detection

Other mods add things pawns should be able to talk about. A detection layer lets a mod's presence
contribute vocabulary, context and events without this mod hard-referencing it.

Shape: a registry of integration profiles, each keyed by package id, activated only when
`ModsConfig.IsActive` says so, contributing context fragments through the same declaration
mechanism as everything else (§2.1) so they inherit the budget and appear in the profile panel.

First targets: **RimTalk Custom Events** (`ethan.rimtalkcustomevents`, ours — so the integration can
be designed from both sides at once) and **Arkhdottir**. Reflection, not hard references: an
integration that crashes when its mod is absent is worse than no integration.

## 16. RJW compatibility

Optional module, gated on `rim.job.world` being active, built **last**.

Last for two reasons. It is the only feature here with a hard dependency on a mod most players do
not run, so it must be cleanly separable; and it is the one place where context leaking into the
wrong prompt is most obviously undesirable, which means it wants the earshot and scoping work from
P3 finished first rather than bolted on.

Prior art already installed: `RimtalkRJW2` and `kuwa.RJWSexInteractionReport`.
