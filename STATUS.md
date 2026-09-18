# Status

**The only place progress is recorded.** Scope and task lists live in [PILLARS.md](PILLARS.md);
what each feature *is* lives in [DESIGN.md](DESIGN.md). If this disagrees with those, this is the
file to fix.

Last updated: **2026-09-17**

| | |
|---|---|
| Builds | **yes** — `.\build.ps1`, 0 warnings, 0 errors, `Arkh.dll` |
| Smoke test | **passing** — `.\tools\smoke-test.ps1`, 14 checks |
| Dependencies | **Harmony only.** No RimTalk, no other mod. |
| Loaded in RimWorld | **never** |
| Repo | [EHtut/Rimtalk-Memories-](https://github.com/EHtut/Rimtalk-Memories-) — name now lags the mod |

---

## Pillars

| | Pillar | State | Notes |
|---|---|---|---|
| A | P1 Core | **done, unverified** | Settings, budget, slots, catalogue, panel, harness. Never run in game. |
| A | P2 Model client | **mostly built** | Mock + OpenAI-compatible working, 22 harness checks. Gemini, Player2 and a test-connection button remain. |
| A | P3 Talk engine | **next** | Has a working client to call now, mock included. |
| A | P4 Display | not started | |
| B | P5 Context and prompt | partly built | Age, gender, world lore done. Colony lore, persona, instruction slots not started. |
| B | P6 Earshot and delivery | not started | |
| C | P7 Memory core | not started | Wants P6's earshot for witness detection. |
| C | P8 Recall | not started | |
| C | P9 Smart context | not started | Shares P8's scorer. |
| D | P10 Dialogue templates | not started | Needs design before code. |
| D | P11 Actions | **unblocked** | Reference landed (`references/Actions`), not yet read. |
| D | P12 Literature and quests | not started | Depends on P2 only — can be pulled forward. |
| E | P13 Mod integration | not started | |
| E | P14 RJW | not started | Deliberately last. |

## Decisions made

- **Standalone, not a RimTalk companion** ([DESIGN.md](DESIGN.md) §2). Reversed an earlier
  decision. The deciding argument: RimTalk's prompt is a player-owned preset, so displacing its
  content meant either asking players to enable advanced mode and delete entries by hand, or
  rewriting their configuration behind their back.
- **Renamed to Arkh**, packageId `ethan.arkh`. A working name — cheap to change while pre-release,
  breaking after a Workshop upload.
- **Incompatible with RimTalk**, declared in About.xml and warned loudly at startup. Both generate
  dialogue for the same colonists.
- **All seven providers eventually**, OpenAI-compatible first because one implementation covers
  six of them ([DESIGN.md](DESIGN.md) §17).
- **Budget order** ([DESIGN.md](DESIGN.md) §12): memory, then how they speak, then what everyone
  knows.

## The honest state

The mod builds, depends on nothing but Harmony, and the harness passes 36 checks — including a full
round trip through the mock provider, request shaping, response parsing and every failure branch.
**No line of it has run in RimWorld.**

What exists is a prompt that can be composed, budgeted and inspected, and a client that can send
it. What is missing is the part that decides *who speaks and when* (P3) and the part that shows the
answer (P4).

Two runtime-only bugs have been caught by the harness and none by the compiler, both the same
family: `String.TrimEnd()` and `String.TrimEnd(char)` are .NET Core additions present in the
reference assemblies and absent from the game's runtime. Assume any convenient BCL overload newer
than .NET Framework 4.8 is a trap.

## Next action

**P3 — the talk engine.** P2 left a working client behind, mock included, so P3 can be built and
demonstrated end to end with no key and no bill.

The remaining P2 items are deliberately deferred rather than forgotten
([docs/specs/P2-model-client.md](docs/specs/P2-model-client.md)); the **test-connection button** is
the one worth doing soon, because right now the only way to find out whether a real key works is to
load a colony.

## Known gaps

- **P11 has no design**, though the reference that unblocks it has arrived and not been read.
- Open questions are tracked in [DESIGN.md](DESIGN.md) §11.
- The GitHub repo is still named `Rimtalk-Memories-`. Renaming it is Ethan's to do; the git remote
  needs updating afterwards.
