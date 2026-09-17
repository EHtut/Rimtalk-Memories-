# P1 — Foundation: working spec

How to finish Pillar 1. Scope, task list and exit criteria are in
[../../PILLARS.md](../../PILLARS.md) P1 and are not repeated here — this file is the *how*.

Three tasks remain. Do them in this order: 1.1 answers whether the foundation works at all, and
1.2 and 1.3 are both much cheaper to build once 1.1 has told us the truth.

---

## 1.1 — Prove the anchors fire

### The problem

We inject three sections: `Pawn:age`, `Pawn:gender`, `Environment:time`. RimTalk's
`ContextBuilder` routes only some categories through the hook path — five pawn categories appear
explicitly in `AppendWithHook` call sites (`location`, `terrain`, `beauty`, `cleanliness`,
`surroundings`), and the rest go through a generic path we have read but not watched run.

If an anchor is never reached, the section is **silently dead**: no exception, no log line, no
text. Everything in P2 would then be built on sand. This has to be settled first and settled
definitively.

### Approach: count invocations in the seam

`RimTalkApi.Guarded` already wraps every provider — it is the one place every context call
passes through. Add a hit counter there, keyed by the label it already receives.

```csharp
// In RimTalkApi
private static readonly Dictionary<string, int> HitCounts = new Dictionary<string, int>();
public static IReadOnlyDictionary<string, int> ProviderHits => HitCounts;
```

Increment inside `Guarded` before invoking the provider, and record separately whether the return
was empty — "called but returned nothing" and "never called" are completely different failures
and must not look the same.

Suggested shape: count **calls** and **non-empty returns** per label.

This is permanent instrumentation, not a temporary probe. It costs one dictionary increment on a
path that already exists, it answers the same question again after every RimTalk update, and 1.2
consumes it directly.

Surface it through a RimWorld debug action (`[DebugAction("RimTalk Memories", ...)]`) that logs
the table. A window is 1.2's job; this only needs to be readable.

### Verification procedure

1. Dev mode on. Load a colony containing a **child, an adult and an elder** — the age bands must
   differ or the test proves nothing. Gender voice needs enabling and both boxes filling, since it
   ships off and empty by design.
2. Put two pawns together and let them talk. Force it through RimTalk's own talk UI if waiting is
   slow.
3. Run the debug action. Every registered label should show a non-zero call count.
4. Open RimTalk's API log (`Dialog_ApiLog`; `ApiHistory.GetAll()` returns `ApiLog` entries, each
   holding the `TalkRequest` with the fully assembled `.Prompt`). Read the actual prompt text.
5. Confirm for each section: it is present, it is in the expected position relative to RimTalk's
   own line for that category, and world lore appears **once** rather than once per participant.

### If an anchor does not fire

In order of preference:

1. **Move the anchor.** Try a neighbouring category known to be wired — for age, `Pawn:profile`
   or `Pawn:personality` are plausible; confirm by counter, not by hope.
2. **Switch from inject to hook.** `RegisterPawnHook` with `HookOperation.Append` runs through
   `ApplyPawnHooks`, which is a different call path from `GetInjectedSectionsAt` and may be reached
   where injection is not.
3. **Harmony, last.** If it comes to this, record it in [../../DESIGN.md](../../DESIGN.md) §10
   with its reason, same as any other patch.

Whatever the outcome, correct [../RIMTALK-API.md](../RIMTALK-API.md) §2 — it currently says these
anchors are unverified, and that sentence must stop being true in one direction or the other.

---

## 1.2 — Prompt inspector

### What it has to answer

Everything after P1 is tuning text inside a prompt. Four questions, repeatedly:

- What did this mod contribute to the last prompt?
- What did the whole assembled prompt end up being?
- What did each contribution cost?
- Which providers fired, and which returned empty?

### Capture

Two sources, because neither alone is enough:

- **Ours.** A bounded ring buffer (~20 entries) written from `Guarded`: label, returned text,
  tick, and the pawn or map it was called for. This is the only place that knows what *we*
  produced, including the empty returns that never reach the prompt.
- **RimTalk's.** `ApiHistory.GetAll()` for the assembled prompt. Do not reconstruct it ourselves —
  RimTalk already has the real thing, and a reconstruction would drift.

Keep the ring buffer allocation-free in the steady state: fixed-size array, overwrite oldest. It
is written on the tick path.

### Window

A `Verse.Window`, opened from a debug action. Two panes:

- **Left** — the provider table: label, calls, non-empty returns, chars last time, chars as a
  share of the prompt. This is 1.1's counter grown a UI.
- **Right** — the last assembled prompt, scrollable, with our contributions highlighted. Falling
  back to plain text when a contribution cannot be located in the final string is fine; do not
  spend effort on exact-match highlighting.

Gate on `Prefs.DevMode` for now. It can graduate to a player-facing tool later if it earns it.

### Deliberately not in scope

Editing prompts from this window. It is an inspector. Editing belongs to RimTalk's own preset UI,
and duplicating that is how we end up maintaining two.

---

## 1.3 — Decide who owns the token budget

### The decision

Every feature in this mod adds prompt text. Right now `WorldLoreMaxChars` caps world lore and
nothing caps the total. Eight features each capped "reasonably" in isolation still add up to a
prompt nobody intended and a bill nobody predicted.

**This has to be settled in P1, not later**, and the reason is a signature. A budget only works if
a provider can be asked for *at most N characters*. RimTalk's provider type is
`Func<Pawn, string>` — no budget parameter, and not ours to change. So we need our own provider
abstraction underneath the seam, something like `Func<Pawn, int, string>`, which `RimTalkApi`
adapts into RimTalk's shape.

Retrofitting that across two providers is an afternoon. Across a dozen, after P2 through P5, it is
a rewrite.

### Options

| | Approach | Trade |
|---|---|---|
| A | Per-feature caps only (today) | Simplest; no global ceiling, so the total is unbounded |
| B | Central budget, fixed allocation per feature | Predictable; wastes the allocation a quiet feature does not use |
| C | Central budget, priority-ordered greedy fill | Unused allocation flows to lower-priority features; needs a priority order maintained |

**Recommendation: C.** Features declare a priority and a desired size; the budget fills in
priority order until exhausted. It degrades in the right direction — when a pawn has a great deal
going on, the low-priority background text is what gets squeezed, not the memory that made this
conversation worth having.

Priority order is itself a design statement and belongs in [../../DESIGN.md](../../DESIGN.md) once
chosen.

### Measuring

Count **characters**, not tokens. Real tokenization is not affordable on the tick path and varies
by provider.

Document the proxy where the budget lives, and its limits: roughly 4 characters per token for
English, but RimTalk is multi-language and Chinese, Japanese and Korean run far denser per
character. A character budget tuned on English will under-count badly for a CJK player. Either
scale the ratio by `Constant.Lang`, or state plainly that the budget is a character budget and let
the player set it — the honest option, and probably the right one.

---

## Done when

The exit criteria in [../../PILLARS.md](../../PILLARS.md) P1 are met, and:

- [../RIMTALK-API.md](../RIMTALK-API.md) §2 states what was actually observed about the anchors
- [../../DESIGN.md](../../DESIGN.md) records the budget decision and the priority order
- [../../STATUS.md](../../STATUS.md) reflects reality

## Risks

- **The generic hook path may not reach our anchors.** The whole mitigation ladder is in 1.1. This
  is the one real risk in P1; everything else here is construction.
- **Providers run on the tick path** ([../RIMTALK-API.md](../RIMTALK-API.md) §4). The ring buffer
  and counters are on it too. Keep them allocation-free.
- **RimTalk 1.2.14 is a moving target.** Everything here reads off one decompiled version. The
  provider hit counter is the early-warning system for the next version — if counts go to zero
  after an update, that is the cause.
