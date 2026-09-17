# P1 — Foundation: working spec

How to finish Pillar 1. Scope and exit criteria are in [../../PILLARS.md](../../PILLARS.md) P1 and
are not repeated here — this file is the *how*.

## Two constraints that set the order

**RimWorld takes ~20 minutes to load.** So there is no "load and check" inner loop. There is one
verification run at the end of the session, and everything needed to read its result must already
exist before it starts. Build the instrument, then run the experiment — not the other way round.

**A merged prompt cannot tell you who wrote what.** RimTalk ships its own prompts; ours are
layered on top. Reading the assembled prompt and trying to spot our contributions is guesswork,
and it gets worse as the layer grows. The fix is not to diff the output — it is to **print our own
layer directly**, from the declarations that produced it.

Those two together mean the injection profile panel is task one, not task two.

Revised order: **1.1 profile panel → 1.2 per-anchor mode → 1.3 budget → 1.4 the one test run.**

---

## 1.1 — Injection profile panel, in Mod Options

### Why Mod Options rather than a dev window

RimWorld **mod settings load at startup**, so this panel is reachable from the main menu without
loading a save. That matters enormously at 20 minutes a load.

It is not merely convenient — most of what we need to check is static. The exact text for each age
band, the world lore string, which anchor each section targets, what it all costs in characters:
none of that needs a pawn. It can be read, reviewed and corrected from the main menu, leaving the
single expensive run to answer only the questions that genuinely require a live game.

Confirmed reachable without a save: `PromptManager.SetInstance(PromptSystem)` is called from
`RimTalkSettings.ExposeData` — RimTalk's prompt system lives in its **mod settings**, not its save
data. `PromptManager.Instance.GetActivePreset().Entries` therefore resolves at the main menu, and
`GetActivePreset()` calls `EnsureInitialized()` when empty, so it self-heals.

Not reachable without a save: `PromptManager.LastContext` (private set, populated only during real
prompt building) and therefore `PresetPreviewGenerator.GeneratePreview`, which returns
*"PreviewNotAvailable"* unless `Current.ProgramState == ProgramState.Playing` **and** a recent
interaction exists. Do not build on it for the offline view.

### A registry is the source of truth, not the output

`RimTalkApi` should record every registration it makes, in a list it owns:

```csharp
public sealed class InjectionDeclaration
{
    public string SectionName;
    public ContextCategory Anchor;
    public InjectionMode Mode;        // Append | Prepend | Override
    public Func<string> DescribeSelf; // the text, or a per-variant sample
    public int Priority;
}

public static IReadOnlyList<InjectionDeclaration> Registrations { get; }
```

The panel renders *this*. It never parses a prompt to work out what we did, so it cannot be wrong
about it — which is the whole point given the merged-prompt problem above.

### Panel contents

**A — RimTalk's native prompt.** Active preset name, and its entries in order: name, role,
position, and the Scriban template (collapsed by default, expandable). Straight from
`GetActivePreset().Entries`. This is the thing we are layering onto, shown plainly.

**B — Our layer.** One row per declaration: anchor, mode, section name, and the exact text that
would be emitted. For variant-driven sections show every variant — all five age bands, both
genders — because "what will a child actually get" is the question being asked, and it should not
require finding a child.

**C — Cost.** Characters per item, total added per prompt, and a worst case. See 1.3 for why
characters and not tokens.

**D — Live, only when a game is running.** Provider hit counts, last emitted value per section,
and the assembled prompt from `ApiHistory.GetAll()` — each `ApiLog` holds the `TalkRequest` with
the full `.Prompt`. Greyed out with a clear reason at the main menu, never silently empty.

### Hit counting

`RimTalkApi.Guarded` already wraps every provider — the one place every context call passes
through. Count **calls** and **non-empty returns** separately: "never called" and "called but
returned nothing" are different failures and must not look alike.

Permanent instrumentation, not a probe. One dictionary increment on a path that already exists,
and it re-answers the question after every RimTalk update — if counts fall to zero after an
update, that is the cause.

Keep it allocation-free: providers run on the tick path
([../RIMTALK-API.md](../RIMTALK-API.md) §4).

---

## 1.2 — Per-anchor mode: append, prepend, or override

Right now every section appends. That is not always what we want.

RimTalk already writes the pawn's age. We then append a line about how someone that age speaks, so
age is mentioned twice — once as RimTalk's bare fact, once as ours. Overriding the category
replaces RimTalk's line with a single richer one instead.

`ContextHookRegistry.HookOperation` supports `Append`, `Prepend` and `Override`, and
`RegisterPawnHook` routes through `ApplyPawnHooks` — a **different call path** from
`GetInjectedSectionsAt`. That second fact is useful beyond tidiness: if an anchor turns out to be
unreachable by injection, the hook path may still reach it.

### The trade, stated plainly

Override means **we own that text forever**. Improvements RimTalk makes to its own age line stop
reaching us, silently. Append means duplication but keeps us downstream of upstream's work.

Recommendation: `Override` for age (the duplication is real and visible), `Append` elsewhere,
every anchor's mode visible in panel section B so the choice is never invisible.

### Work

- Add `RegisterPawnHook` / `RegisterEnvironmentHook` wrappers to `RimTalkApi`, guarded the same way
- One declaration model covering both paths, so the panel enumerates them uniformly
- Make mode a per-section decision recorded in `ContextRegistrar`
- Record each `Override` in [../../DESIGN.md](../../DESIGN.md) §5/§6 with its reason

---

## 1.3 — Decide who owns the token budget

### The decision

Every feature adds prompt text. `WorldLoreMaxChars` caps world lore; nothing caps the total. Eight
features each capped "reasonably" in isolation still add up to a prompt nobody intended.

**Settle it now, and the reason is a signature.** A budget only works if a provider can be asked
for *at most N characters*. RimTalk's provider type is `Func<Pawn, string>` — no budget parameter,
and not ours to change. So our own provider abstraction underneath the seam needs the budget in
its signature, with `RimTalkApi` adapting it into RimTalk's shape. That is an afternoon across two
providers and a rewrite across a dozen after P2–P5.

It also folds neatly into 1.1: the declaration registry is already the place a budget would read
priorities and sizes from.

| | Approach | Trade |
|---|---|---|
| A | Per-feature caps only (today) | Simplest; total is unbounded |
| B | Central budget, fixed allocation per feature | Predictable; wastes what a quiet feature does not use |
| C | Central budget, priority-ordered greedy fill | Unused allocation flows down the priority order; needs that order maintained |

**Recommendation: C.** It degrades in the right direction — when a pawn has a great deal going on,
the background text gets squeezed rather than the memory that made the conversation worth having.

Priority order is a design statement; put it in [../../DESIGN.md](../../DESIGN.md) once chosen.

### Measuring

Count **characters**, not tokens — real tokenization is not affordable on the tick path and varies
by provider. Roughly 4 characters per token for English, but RimTalk is multi-language and CJK
runs far denser per character, so an English-tuned budget under-counts badly for those players.
Either scale by `Constant.Lang` or call it a character budget and let the player set it. The
latter is honest and probably right.

---

## 1.4 — The one verification run

Everything above ships before this happens. Then load **once** and work the whole checklist.

### Before loading

- Clean build, zero warnings
- Panel opens from the main menu and sections A–C are populated and correct
- Every band and variant in section B reads the way it should — fix text here, not after loading

### Setup

A colony containing a **child, an adult and an elder** — the bands must differ or the test proves
nothing. Enable gender voice and fill both boxes, since it ships off and empty by design.

### Checklist

1. Startup log lists registered providers; the conflict warning fires (four superseded mods are
   installed on this machine, so it should be immediate).
2. Panel section D populates: every registered section shows a non-zero **call** count. Any zero
   means that anchor is not reached — see the ladder below.
3. Non-empty returns are non-zero where expected, and zero for adult age voice (empty by design)
   and for gender voice if left unfilled. A mismatch here is a content bug, not a plumbing bug.
4. Assembled prompt in section D contains each section's text, positioned as intended relative to
   RimTalk's own line for that category.
5. World lore appears **once** per prompt, not once per participant.
6. Age-overridden category shows our line and **not** a duplicate bare age.
7. Character costs in section C match what section D actually emitted.

### If an anchor shows zero calls

1. **Switch that anchor to a hook** (`RegisterPawnHook`, `HookOperation.Append`) — different call
   path, may be reached where injection is not. This is why 1.2 exists first.
2. **Move the anchor** to a neighbouring category. Confirm by counter, never by hope.
3. **Harmony, last.** Record it in [../../DESIGN.md](../../DESIGN.md) §10 with its reason.

Whatever happens, correct [../RIMTALK-API.md](../RIMTALK-API.md) §2 — it currently says these
anchors are unverified, and that must stop being true in one direction or the other.

---

## Done when

[../../PILLARS.md](../../PILLARS.md) P1's exit criteria are met, and:

- [../RIMTALK-API.md](../RIMTALK-API.md) §2 states what was actually observed
- [../../DESIGN.md](../../DESIGN.md) records the budget decision, the priority order, and every
  `Override` with its reason
- [../../STATUS.md](../../STATUS.md) reflects reality

## Risks

- **An anchor may be unreachable.** The one real risk in P1; ladder in 1.4. Everything else is
  construction.
- **Override is a one-way door in practice.** Easy to add, easy to forget, and it silently cuts us
  off from upstream improvements to that text. Panel section B existing is the mitigation.
- **RimTalk 1.2.14 is a moving target.** Everything here reads off one decompiled version. The hit
  counter is the early-warning system.
