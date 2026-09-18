# Status

**The only place progress is recorded.** Scope and task lists live in [PILLARS.md](PILLARS.md);
what each feature *is* lives in [DESIGN.md](DESIGN.md). This file says how far along we are and
nothing else — if it disagrees with those, this file is the one to fix.

Last updated: **2026-09-17**

| | |
|---|---|
| Builds | **yes** — `.\build.ps1`, 0 warnings, 0 errors |
| Smoke test | **passing** — `.\tools\smoke-test.ps1`, 13 checks |
| Loaded in RimWorld | **never** |
| Repo | [EHtut/Rimtalk-Memories-](https://github.com/EHtut/Rimtalk-Memories-) |

---

## Pillars

| Pillar | State | Notes |
|---|---|---|
| P1 Foundation | **one task left** | Everything built. Only the verification run remains. |
| P2 Voice and lore | in progress | Age, gender, world lore built. Colony lore, per-save override, prompt entries, starter preset not started. |
| P3 Earshot and delivery | not started | First pillar needing Harmony. |
| P4 Memory core | not started | Wants P3's earshot model for witness detection. |
| P5 Recall | not started | |
| P6 Smart context | not started | Shares P5's relevance scorer. |
| P7 Dialogue templates | not started | Needs design before code. |
| P8 Actions | **blocked** | Waiting on reference material. Scope ambiguous — [PILLARS.md](PILLARS.md) P8. |
| P9 Literature and quests | not started | **Independent of every other pillar** — can be pulled forward at any time. |
| P10 Mod integration | not started | RimTalk Custom Events, Arkhdottir. |
| P11 RJW compatibility | not started | Deliberately last. |

## Decisions made

- **Not forking RimTalk.** Considered and declined — [DESIGN.md](DESIGN.md) §2.2 has the reasoning
  and the conditions under which to revisit it. The `RimTalkApi` seam is what keeps the option open.
- **Prompt injection through the API is fully dynamic** ([DESIGN.md](DESIGN.md) §2.1). Preset entry
  content is rendered through Scriban at build time and resolves variables other mods registered,
  so a prompt entry plus a registered context variable is a block we place ourselves, anywhere in
  the message list, filled fresh on every prompt.
- **Budget order** ([DESIGN.md](DESIGN.md) §12): memory, then how they speak, then what everyone
  knows.
- **RimTalk is transport; the content is ours** ([DESIGN.md](DESIGN.md) §2.1a). Its whole prompt is
  five preset entries. Four are ours to replace — its voice, its context dump, its chat-history
  "memory". Only the JSONL format entry must survive, because RimTalk parses against it. Persona
  goes via an `Override` hook on `Pawn:personality`.
- **Advanced prompt mode is the gate.** `UseAdvancedPromptMode` is off by default; in simple mode
  RimTalk re-inserts its built-in entries, so our additions survive but our removals do not. P2
  owns detecting it and offering to turn it on.

## The honest state of P1 and P2

Everything written compiles against the real RimTalk assembly; decompiling our own output confirms
all four attachment modes reach the intended RimTalk entry points at the intended anchors; and the
smoke test drives the budget allocator, text clamping and variant enumeration with no game loaded.
That is as far as static checking goes. **No line of this mod has run in RimWorld.**

Two unknowns remain that only a running game can settle:

1. **Do the anchors fire?** We attach at `Pawn:age`, `Pawn:gender` and `Environment:time`. A
   section at a category RimTalk never reaches is silently dead — no error, no text, no clue. The
   panel's Live tab answers exactly this, and distinguishes "never called" from "called but
   returned nothing".
2. **Does the folded text read well?** Age folds into RimTalk's own age value now, so a template
   using `{{pawn.age}}` gets `34 — speaks from long experience…` inline. If it reads badly, the
   mode is a setting — switch it in the panel without rebuilding.

## Next action

**P1's verification run** — the last task in the pillar. Checklist in
[docs/specs/P1-foundation.md](docs/specs/P1-foundation.md) §1.4. Run `tools/smoke-test.ps1`, then
open the panel from the main menu and correct the wording there, and only then load a colony.

After that, P2 or P9 — P9 depends on nothing and is a change of pace.

## Known gaps

- **P8 has no design.** It was in the original brief and is still only two candidate readings.
- Open design questions are tracked in [DESIGN.md](DESIGN.md) §11.
