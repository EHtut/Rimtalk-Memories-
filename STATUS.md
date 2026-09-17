# Status

**The only place progress is recorded.** Scope and task lists live in [PILLARS.md](PILLARS.md);
what each feature *is* lives in [DESIGN.md](DESIGN.md). This file says how far along we are and
nothing else — if it disagrees with those, this file is the one to fix.

Last updated: **2026-09-17**

| | |
|---|---|
| Builds | **yes** — `.\build.ps1`, 0 warnings, 0 errors |
| Loaded in RimWorld | **never** |
| Git | initialized, **no commits yet**, remote pending |

---

## Pillars

| Pillar | State | Notes |
|---|---|---|
| P1 Foundation | **in progress** | Code written; nothing verified in game. 3 tasks left. |
| P2 Voice and lore | **in progress** | Age, gender, world lore written. Colony lore, per-save override, prompt entries and starter preset not started. |
| P3 Earshot and delivery | not started | First pillar that needs Harmony. |
| P4 Memory core | not started | Wants P3's earshot model for witness detection. |
| P5 Recall | not started | |
| P6 Dialogue templates | not started | Needs design work before code. |
| P7 Actions | **blocked** | Waiting on reference material from Ethan. Scope ambiguous — see [PILLARS.md](PILLARS.md) P7. |

## The honest state of P1 and P2

Everything marked written compiles against the real RimTalk assembly. That proves the API calls
are type-correct and **nothing else**. No line of this mod has run.

Three specific unknowns, in the order they would hurt:

1. **Do the anchors fire?** Sections are injected at `Pawn:age`, `Pawn:gender` and
   `Environment:time`. Only some pawn categories are demonstrably wired through RimTalk's hook
   path ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §2). A section at a category RimTalk never
   reaches is silently dead — no error, no text, no clue.
2. **Does the text land where intended?** Before/after ordering at an anchor is understood from
   the decompilation, not observed.
3. **Does the conflict warning fire?** Four superseded mods are installed on this machine, so it
   should be immediate and loud.

## Next action

P1's remaining tasks, in [PILLARS.md](PILLARS.md) order — load the mod and check the anchors,
then build the prompt debug window before P2 continues.

## Known gaps

- **P7 has no design at all.** It was in the original brief and is not yet written up anywhere
  beyond the two candidate readings in [PILLARS.md](PILLARS.md).
- Open design questions — colony lore scoping, caravan persistence, token budget ownership — are
  tracked in [DESIGN.md](DESIGN.md) §11.
