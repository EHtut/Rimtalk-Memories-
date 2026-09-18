# RimTalk's extension surface

Reference notes for the RimTalk API this mod builds on. **This is the only place these facts are
written down** — design decisions that depend on them live in [../DESIGN.md](../DESIGN.md) and
cite this file rather than restating it.

Established by decompiling `RimTalk.dll` **v1.2.14** (Workshop id `3551203752`, `cj.rimtalk`).
RimTalk ships no source and documents none of this, so everything here is read off the shipped
assembly and will need rechecking when RimTalk updates. Line numbers refer to that decompilation
and are evidence, not something to navigate by.

## 1. Entry point

`RimTalk.API.RimTalkPromptAPI` — static, public, and clearly intended for other mods: every
method takes a `modId`, and `UnregisterAllHooks(modId)` takes it all back.

Four things it can do:

| Capability | Methods |
|---|---|
| Inject a block of text next to a section RimTalk already builds | `InjectPawnSection`, `InjectEnvironmentSection` |
| Modify a section RimTalk already built | `RegisterPawnHook`, `RegisterEnvironmentHook` |
| Add a template variable players can use in presets | `RegisterPawnVariable`, `RegisterEnvironmentVariable`, `RegisterContextVariable` |
| Edit the active prompt preset | `AddPromptEntry`, `InsertPromptEntryBefore/AfterName`, `RemovePromptEntriesByModId` |

Supporting types `ContextHookRegistry.HookOperation` (`Append`, `Prepend`, `Override`) and
`ContextHookRegistry.InjectPosition` (`Before`, `After`) are **nested public** and usable from
outside. `ContextCategory` is a **struct** (`TryGetPawnCategory` returns `ContextCategory?`) with a
public constructor `(string key, ContextType type)`, so custom anchors are possible — though
nothing in RimTalk would build them.

### 1.1 Hooks reach further than injections

These are two different mechanisms with two different reaches, and the difference matters when
choosing how to attach something.

**Injections** add a new block near a category. They are consumed **only** in `AppendWithHook` and
`ApplyEnvironmentWithHook` (~14793–14845), which is `ContextBuilder`'s prose-assembly path.

**Hooks** transform the category's own value, and `ApplyPawnHooks` is called from **two** places:
that same prose path (~14805), *and* Scriban variable resolution (~17250). So when a preset
template references `{{pawn.age}}`, RimTalk computes the value and then runs hooks over it —
whereas an injection at `Pawn:age` does not fire there at all.

**A hook therefore rides along wherever RimTalk puts that category, including inside
player-authored templates.** An injection only appears where `ContextBuilder` assembles prose. When
in doubt, hook.

### 1.2 Hook semantics — the names are misleading

`ApplyPawnHooks` (~25202):

- A **handler is `Func<Pawn, string, string>`**: it receives the running value and returns the
  complete new value. `Append` and `Prepend` name **the order handlers run in**, not a string
  operation — RimTalk does not concatenate anything for you. An "append" hook that wants the
  original kept must return `original + something` itself.
- **`Override` returns on the first non-null result** and skips prepend and append entirely. The
  handler still receives the original value, so "override" can mean *replace with a function of
  the original* rather than discarding it — usually what you actually want.
- Order is: all Override (first non-null wins) → all Prepend → all Append.
- Every handler is individually try/caught by RimTalk, which logs and moves on. That is a softer
  net than the provider path, which has none — but do not rely on it.

## 2. Anchors

Injection and hooks attach to a `ContextCategory`. The full set, from
`RimTalk.API.ContextCategories`:

**Pawn** — `name`, `fullname`, `gender`, `age`, `race`, `title`, `faction`, `role`, `job`,
`personality`, `mood`, `moodpercent`, `profile`, `backstory`, `traits`, `skills`, `health`,
`thoughts`, `social`, `fullsocial`, `fullrelation`, `fullinteraction`, `fullthought`, `equipment`,
`genes`, `notable_genes`, `ideology`, `captive_status`, `location`, `terrain`, `beauty`,
`cleanliness`, `surroundings`

**Environment** — `time`, `date`, `season`, `weather`, `temperature`, `wealth`, `events`

Note that only a handful of pawn categories are actually wired through `AppendWithHook` in
`ContextBuilder` (`location`, `terrain`, `beauty`, `cleanliness`, `surroundings` appear there
explicitly). The rest route through the generic path at ~14793. **Verify an anchor fires before
depending on it** — a section injected at a category RimTalk never reaches is silently dead.

## 3. What a provider returns

A provider is `Func<Pawn, string>` or `Func<Map, string>`. RimTalk calls it and appends the result
with `AppendLine`, verbatim.

- **Empty or null is skipped entirely** (`AppendIfNotEmpty`, ~14783). Returning `""` is the
  correct way to say nothing — it costs no tokens and leaves no blank line.
- **The provider owns its own label.** RimTalk adds no heading, prefix or punctuation.
- Ordering at one anchor is: all `Before` providers → RimTalk's own text (after hooks) → all
  `After` providers.

## 4. Providers run on the main thread, inside the tick

This is the constraint that shapes everything expensive.

In `TalkService` the prompt is assembled and *then* dispatched:

```
15465:  talkRequest.PromptMessages = PromptManager.Instance.BuildMessages(talkRequest, list, text);
15471:  Task.Run(() => GenerateAndProcessTalkAsync(talkRequest));
```

`BuildMessages` → `PromptService.BuildContext` → the providers. All of it happens before
`Task.Run`, synchronously, on the calling thread.

Two consequences, and they pull in opposite directions from what you might assume:

1. **Game state is safe to touch.** No cross-thread access worries; `Find`, `Map`, pawn state are
   all fine.
2. **It is on the tick budget.** A slow provider is a frame hitch every time any pawn speaks.
   Lookups and cached values only — never real work.

RimTalk wraps none of this in a try/catch of its own, so an exception escaping a provider takes
down the pawn's talk request. `RimTalkApi.Guarded` catches and returns `""` instead.

## 5. Mod id sanitization — a real trap

`RimTalkPromptAPI` runs mod ids through:

```csharp
Regex.Replace(modId.ToLowerInvariant(), "[^a-z0-9]", "")
```

before storing them — for hooks, variables and injections, and for `UnregisterAllHooks`.

But `RemovePromptEntriesByModId` does **not** sanitize; it compares `e.SourceModId == modId`
raw. And `CreatePromptEntry` stores whatever it is given.

So `"Ethan.RimTalkMemories"` would register hooks under `ethanrimtalkmemories` while prompt
entries stayed under the original string — and removal by one id would miss the other.
**Use an already-lowercase, alphanumeric-only id everywhere**, which is why `RimTalkApi.ModId` is
`"rimtalkmemories"`.

## 6. Prompt presets need a loaded game

`AddPromptEntry` and every `InsertPromptEntry*` resolve
`PromptManager.Instance.GetActivePreset()`. Before a save is loaded that is null: the call logs a
warning and returns `false`.

Hooks, variables and injected sections have no such requirement — they go into static registries
and can be registered at startup. See [../DESIGN.md](../DESIGN.md) §3.2.

`PromptEntry` ids are deterministic from `(modId, name)` via `GenerateDeterministicId`, so the
same entry re-registers to the same id across sessions.

### 6.1 Entry content is a live template, not stored text

This is the single most useful fact in this document, and the least obvious.

`BuildMessagesFromPreset` (~18500) assembles the message list like this:

```csharp
return PromptPresetAssembler.AssembleMessages(
    preset,
    (string content) => ScribanParser.Render(content, context),
    chatHistory, segments);
```

**Every entry's content is rendered through Scriban on every prompt build**, and Scriban resolution
consults `ContextHookRegistry` for variables other mods registered (~17203–17265).

So `AddPromptEntry` + `RegisterContextVariable` together give:

- **Arbitrary placement** — before or after any named entry, at any `PromptRole`, `Relative` or
  `InChat` at a chosen depth.
- **Arbitrary content, recomputed per prompt** — the entry stores `{{our_variable}}`; the provider
  runs fresh each time.
- **The whole context**, not just a pawn: `RegisterContextVariable` takes
  `Func<PromptContext, string>`, and `PromptContext` carries `CurrentPawn`, `AllPawns`,
  `Participants`, `TalkType`, `ChatHistory`, `Map`, `IsMonologue` and the rest.

A section rendering empty costs nothing, so an entry can be conditional simply by returning `""`.

This is why attaching to RimTalk does not make prompt injection static, and it is the mechanism to
reach for when an anchor is the wrong shape for what you want to say. See
[../DESIGN.md](../DESIGN.md) §2.1.

## 7. The talk data model

Useful beyond context injection — the memory and response-type work will need these.

- **`Cache.Get(pawn)` → `PawnState`** — RimTalk tracks pawns lazily, so a freshly spawned pawn can
  be absent for a while. Null is normal, not an error.
- **`PawnState`** — `CanGenerateTalk()`, `CanDisplayTalk()`, `AddTalkRequest(prompt, recipient,
  TalkType)`, `TalkRequests` (pending), `TalkResponses` (generated, not yet spoken),
  `TalkInitiationWeight`, `LastTalkTick`.
- **`TalkRequest`** — `Prompt`/`RawPrompt`, `Participants`, `Initiator`/`Recipient`,
  `ConversationId`, `TalkType`, `Status`, `IsMonologue`.
- **`TalkResponse`** — `Text`, `TalkType`, `TargetPawn`, `GetInteractionType()`, `IsReply()`.
- **`TalkHistory`** — `AddMessageHistory`, `GetMessageHistory(pawn, simplified)`.
- **`TalkType`** (fixed enum, not extensible): `Urgent`, `Hediff`, `LevelUp`, `Chitchat`,
  `Interaction`, `Event`, `QuestOffer`, `QuestEnd`, `Thought`, `User`, `Announcement`, `Sleep`,
  `Other`.
- **`InteractionType`**: `None`, `Insult`, `Slight`, `Chat`, `Kind`.

`TalkType` being closed is why whisper/shout/thought have to be this mod's own concept rather than
new enum members ([../DESIGN.md](../DESIGN.md) §9).

## 7.1 Borrowing the AI client for work that is not a conversation

`RimTalk.Client.AIClientFactory.GetAIClientAsync()` is **public** and returns an `IAIClient` bound
to whatever provider, key and model the player already configured in RimTalk:

```csharp
public interface IAIClient
{
    Task<Payload> GetChatCompletionAsync(
        List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages,
        Action<Payload> onRequestPrepared = null);
    // plus image and streaming overloads
}
```

That means a feature which is not a pawn talking — rewriting a book, drafting a quest — does not
have to pretend to be one. It builds its own messages and makes its own request, reusing the
player's configured connection without asking them to set one up twice, and without touching the
prompt pipeline at all.

`Payload` carries `TokenCount` and `ErrorMessage`, so cost and failure are both visible.

This is the basis of [../DESIGN.md](../DESIGN.md) §14 and PILLARS P9, and it is why that pillar
depends on nothing else.

## 8. Prior art in the mods being replaced

What the companions did, and what is worth taking.

- **Context Upgrade** (`wuren.rimtalkcontextupgrade`) — Harmony patches on fourteen RimTalk
  internals including `ContextBuilder`, `PromptService.BuildContext`, `PromptManager.BuildMessages`
  and `TalkResponse.GetText`. Does not use the public API at all. Its *settings* are the useful
  part: separate counts for important vs random thoughts, relations count, health/skill entry
  counts, baby/toddler/child speech. Good evidence of which knobs players actually want.
- **Distance Control** (`youyu.rimtalk.distancecontrol`) — reflection only, no hard reference.
  Patches `CustomDialogueService.CanTalk`, `PawnSelector.GetNearbyPawnsInternal`,
  `ContextHelper.CollectNearbyContext`, and `MemoryThoughtHandler.TryGainMemory` (to suppress the
  "slighted" debuff when a pawn was simply out of earshot — a good catch worth keeping).
  Defaults: talk 20, hearing 10, viewing 20, announcement 30, context distance 5, same-room on.
- **Lucid Chronicle** (`alus.rimtalk.lucidchronicle`) — closest prior art to the memory engine,
  and it documents its own API in `API.md`. Layered memory (pinned / long-term / mid-term /
  today's rolling summary), player-authored world and colony knowledge, earshot-scoped recent
  conversation, and secret lines that reach only their intended hearer. Registers template
  variables `{{pawn.memory}}`, `{{recentchat}}`, `{{innerthought}}`, `{{commonknowledge}}`.
  Worth reading properly before designing §8 in detail.
- **Dialogue Patch** (`neachi.RimTalkDialoguePatch`) — a UI mod for RimTalk's player-facing talk
  window (mode buttons, Tab to switch, Ctrl+Enter to send). **Not** a dialogue template library,
  despite the name. Nothing to absorb for templates.
- **Event+** (`saltgin.rimtalkeventmemory`) — event memory. Not yet examined in detail.
