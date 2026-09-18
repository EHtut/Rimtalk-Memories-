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
| P1 Foundation | **in progress** | Seam, panel and per-anchor mode written. Token budget undecided; verification run outstanding. |
| P2 Voice and lore | **in progress** | Age, gender, world lore written. Colony lore, per-save override, prompt entries and starter preset not started. |
| P3 Earshot and delivery | not started | First pillar that needs Harmony. |
| P4 Memory core | not started | Wants P3's earshot model for witness detection. |
| P5 Recall | not started | |
| P6 Dialogue templates | not started | Needs design work before code. |
| P7 Actions | **blocked** | Waiting on reference material from Ethan. Scope ambiguous — see [PILLARS.md](PILLARS.md) P7. |

## The honest state of P1 and P2

Everything written compiles against the real RimTalk assembly, and decompiling our own output
confirms all four attachment modes reach the intended RimTalk entry points
(`InjectPawnSection`, `InjectEnvironmentSection`, `RegisterPawnHook`, `RegisterEnvironmentHook`)
at the intended anchors. That is as far as static checking goes. **No line of this mod has run.**

Two unknowns remain, and only a running game can settle them:

1. **Do the anchors fire?** We attach at `Pawn:age`, `Pawn:gender` and `Environment:time`. Only
   some categories are demonstrably wired through RimTalk's paths
   ([docs/RIMTALK-API.md](docs/RIMTALK-API.md) §2). A section at a category RimTalk never reaches
   is silently dead — no error, no text, no clue. The panel's Live tab exists to answer exactly
   this, and distinguishes "never called" from "called but returned nothing".
2. **Does the folded text read well?** Age now folds into RimTalk's own age value rather than
   sitting beside it. Whether `34 — speaks from long experience…` reads naturally in place,
   especially inside a template, is a judgement that needs to be seen. If it reads badly, the
   mode is a setting — switch it in the panel without rebuilding.

## Next action

Two things, in either order:

- **Decide who owns the token budget** (P1's last build task) — a decision, not construction, and
  it changes a provider signature, so it is much cheaper now than after P2–P5.
  [docs/specs/P1-foundation.md](docs/specs/P1-foundation.md) §1.3 has the options and a
  recommendation.
- **The one verification run** — checklist in the same spec, §1.4. Open the panel from the main
  menu first and correct the wording there; only then load a colony.

## Known gaps

- **P7 has no design at all.** It was in the original brief and is not yet written up anywhere
  beyond the two candidate readings in [PILLARS.md](PILLARS.md).
- Open design questions — colony lore scoping, caravan persistence, token budget ownership — are
  tracked in [DESIGN.md](DESIGN.md) §11.
