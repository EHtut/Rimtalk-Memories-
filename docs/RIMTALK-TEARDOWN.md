# RimTalk teardown — prior art

RimTalk solved a lot of these problems first. This records what it does and how, as a reference
for building Arkh's own engine.

**This is not an integration guide.** Arkh does not use RimTalk's API; it replaced it
([../DESIGN.md](../DESIGN.md) §2). Everything here is about learning from a working
implementation, not calling into one. The sections about its extension API — anchors, hooks,
mod-id handling — were deleted when that decision was made, because a document that describes
something nobody uses is worse than no document.

Established by decompiling `RimTalk.dll` **v1.2.14** (Workshop `3551203752`, `cj.rimtalk`), which
ships no source. Line numbers refer to that decompilation and are evidence, not navigation.

## 1. Its prompt is five entries

`CreateDefaultPreset()` builds the whole thing:

| # | Name | Role | Content |
|---|---|---|---|
| 1 | Base Instruction | System | a hardcoded behaviour instruction |
| 2 | JSON Format | System | the JSONL contract — `name`, `text`, optional `act`/`target` |
| 3 | Context *(or "Pawn Profiles")* | System | the whole `ContextBuilder` dump |
| 4 | Chat History | User | recent lines |
| 5 | Dialogue Prompt | User | the trigger |

Worth taking: the shape is sound, and separating the output contract from the behaviour
instruction is a good idea we kept ([../DESIGN.md](../DESIGN.md) §4).

Worth improving on: entry 4 is a transcript doing the job of a memory, and entry 3 is an
everything-dump with no notion of what matters now. Those two gaps are most of Arkh's reason to
exist.

The preset is player-editable and persisted in RimTalk's **mod settings**, not the save. That is
where Arkh's own "settings load at startup, so the panel works at the main menu" rule came from
([../DESIGN.md](../DESIGN.md) §3.1) — the idea is worth keeping even though the code is not.

## 2. Context is built whether or not it is used

`talkRequest.Context = PromptService.BuildContext(pawns, ...)` runs unconditionally before the
preset is assembled (~18478), so disabling the entry that displays it does not stop it being
built.

A caution for our own assembler: **build context lazily, per section, driven by what the prompt
actually asks for.** Precomputing everything and then discarding most of it is a cost paid on the
tick path for nothing.

## 3. Prompt assembly is synchronous, on the main thread

```
15465:  talkRequest.PromptMessages = PromptManager.Instance.BuildMessages(...);
15471:  Task.Run(() => GenerateAndProcessTalkAsync(talkRequest));
```

Assemble, *then* dispatch. Game state is safe to read during assembly and nothing needs locking;
the cost is that assembly is on the tick budget.

Arkh follows the same split ([../DESIGN.md](../DESIGN.md) §18) because it is the right one: the
alternative is reading pawn state from a worker thread, which RimWorld does not tolerate.

## 4. Its talk data model

A reasonable vocabulary, worth borrowing in shape if not in code:

- **Per-pawn state** — `CanGenerateTalk()`, pending requests, generated-but-unspoken responses,
  an initiation weight, last-spoken tick. Built lazily, so absence is normal rather than an error.
- **A request** — prompt, participants, initiator/recipient, conversation id, type, status.
- **A response** — text, target pawn, an interaction classification, whether it is a reply.
- **Talk types** — `Urgent`, `Hediff`, `LevelUp`, `Chitchat`, `Interaction`, `Event`, `QuestOffer`,
  `QuestEnd`, `Thought`, `User`, `Announcement`, `Sleep`, `Other`.
- **Interaction types** — `None`, `Insult`, `Slight`, `Chat`, `Kind`, mapped onto RimWorld's social
  effects.

The talk-type list is a good inventory of *occasions for speech* and a useful checklist when
deciding what Arkh should react to.

## 5. Providers

RimTalk supports OpenAI, Gemini, DeepSeek, OpenRouter, Player2, Ollama and LM Studio behind one
`IAIClient` with completion and streaming overloads, plus retry, quota handling and a device-auth
flow for Player2. Its payload type carries token count and error message.

Two things to copy in spirit: **one interface with several implementations** rather than
per-provider branching, and **surfacing token count and error text** so cost and failure are
visible rather than inferred. See [../DESIGN.md](../DESIGN.md) §17.

## 6. Prior art in the companion mods

- **Context Upgrade** (`wuren.rimtalkcontextupgrade`) — Harmony patches on fourteen RimTalk
  internals. Its *settings* are the useful part: separate counts for important vs random thoughts,
  relations count, health and skill entry counts, baby/toddler/child speech. Good evidence of which
  knobs players actually want.
- **Distance Control** (`youyu.rimtalk.distancecontrol`) — reflection only. Defaults: talk 20,
  hearing 10, viewing 20, announcement 30, context radius 5, same-room on. Also patches
  `MemoryThoughtHandler.TryGainMemory` to suppress the "slighted" debuff when a pawn was merely out
  of earshot — a good catch worth keeping ([../DESIGN.md](../DESIGN.md) §9).
- **Lucid Chronicle** (`alus.rimtalk.lucidchronicle`) — closest prior art to the memory engine, and
  it documents its own API. Layered memory (pinned / long-term / mid-term / today's rolling
  summary), player-authored world and colony knowledge, earshot-scoped recent conversation, and
  secret lines that reach only their intended hearer. **Read properly before designing §8 in
  detail.**
- **Dialogue Patch** (`neachi.RimTalkDialoguePatch`) — a UI mod for RimTalk's talk window despite
  the name. Nothing to take for dialogue templates.
- **Expand Literature / Quests** (`cj.rimtalk.literature`, `rimtalk.quests`) — prior art for §14,
  in `references/`.
- **Expand Actions** — in `references/Actions`, and the input that unblocks
  [../PILLARS.md](../PILLARS.md) P11. Not yet read.
