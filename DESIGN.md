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

Every patch added must be listed here as it lands, with its reason. The context layer (P1, P2)
requires none at all, which is the point of §2.

## 11. Open questions

- **Colony lore scoping.** Prisoners and visitors must not speak from colony-internal knowledge.
  Where does the eligibility check live — memory, or context?
- **Memory persistence across colonies.** Almost certainly per-save (`GameComponent`), but a
  pawn who leaves in a caravan and returns must keep theirs.
- **Token budget.** Every feature here adds prompt text. There should be one place that knows the
  total budget and divides it, rather than each feature capping itself in isolation.
