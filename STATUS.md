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
| A | P2 Model client | **next** | The gate on everything: nothing speaks until this exists. |
| A | P3 Talk engine | not started | |
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

The mod builds, has no dependencies beyond Harmony, and the harness passes 14 checks against the
real shipping declarations. **No line of it has run in RimWorld**, and nothing will speak until P2
and P3 exist — that is the cost of the standalone decision, stated in [DESIGN.md](DESIGN.md) §2.1.

What exists today is a prompt that can be composed, budgeted and inspected, with nothing to send
it to.

## Next action

**P2 — the model client**, starting with the mock provider. That ordering is deliberate: the mock
makes P3, P4 and all of Phase C developable with no key, no network and no bill, and lets the
harness exercise the whole pipeline headless.

Write `docs/specs/P2-model-client.md` when starting it. (The old P1 spec was deleted — it described
integrating with RimTalk's anchors, which no longer exists.)

## Known gaps

- **P11 has no design**, though the reference that unblocks it has arrived and not been read.
- Open questions are tracked in [DESIGN.md](DESIGN.md) §11.
- The GitHub repo is still named `Rimtalk-Memories-`. Renaming it is Ethan's to do; the git remote
  needs updating afterwards.
