# Arkh

Colonists who talk, and who remember.

Arkh generates colony dialogue with a language model of your choosing, and builds it around a
memory system rather than a transcript. Memories are scored for weight and for how much they
matter right now, and the ones that belong together surface together — so a colonist recalls the
raid and the person they lost in it in the same breath, not as two disconnected lines.

Around that sits the talking itself: editable prompt blocks, separate voices by age and gender,
shared world and colony lore, distance rules for who can hear what, whisper / shout / thought as
distinct ways of speaking, and a character budget so background text gives way before anything
that matters to the moment.

**RimWorld 1.6. Requires Harmony and [Interaction Bubbles](https://steamcommunity.com/sharedfiles/filedetails/?id=1516158345).**

Bubbles draws the speech. It already does that well and players have already tuned its settings, so
Arkh publishes lines to RimWorld's own social log and lets Bubbles render them — no second set of
controls, and no second bubble.

> ⚠️ Early development. The engine is complete — prompt, model client, talk scheduling and display
> — but **none of it has been run in RimWorld yet**. [STATUS.md](STATUS.md) is the honest account.

## Not compatible with RimTalk

Arkh **replaces** RimTalk rather than extending it. Running both means two mods generating
dialogue for the same colonists, and paying for every line twice. This is declared in About.xml
and warned about at startup; it will not disable anything for you.

Arkh began as a RimTalk companion and stopped being one. [DESIGN.md](DESIGN.md) §2 has the
reasoning — briefly, RimTalk's prompt is a player-owned preset, so replacing its content meant
either asking players to edit it by hand or rewriting their config behind their back.

These RimTalk companions are also superseded, and do nothing for Arkh:

| Mod | Package id | Covered by |
|---|---|---|
| RimTalk Context Upgrade | `wuren.rimtalkcontextupgrade` | Context and prompt settings |
| RimTalk Event+ | `saltgin.rimtalkeventmemory` | Event memory |
| RimTalk Lucid Chronicle | `alus.rimtalk.lucidchronicle` | Long-term memory and world lore |
| RimTalk Distance Control | `youyu.rimtalk.distancecontrol` | Conversation distance |

Keep this table in step with `Superseded` in
[Source/Integration/ConflictDetector.cs](Source/Integration/ConflictDetector.cs).

## Building

The project lives inside `RimWorld/Mods/`, so it builds straight into place — no copy step.

```bash
./build.ps1
```

Needs the .NET SDK. Game assemblies come from NuGet reference packages, so no RimWorld install is
required to compile. `-Configuration Debug` for a debug build. Restart RimWorld to pick up a new
assembly.

Before any in-game test, run the headless harness — it drives the built assembly with no game
loaded, and has already caught four bugs that would each have cost a twenty-minute load to find:

```bash
./tools/smoke-test.ps1
```

The compiled DLL **is** committed — players install this repo as the mod folder and most will not
have a .NET SDK.

## Layout

```
About/            Mod manifest
Source/
  Model/          Talking to a language model, and failing well when we cannot
  Prompt/         Prompt slots, the section catalogue, assembly
  Context/        What goes in the blocks (age, gender, world lore)
  Talk/           Who speaks and when; parsing what comes back
  Display/        The bridge to Interaction Bubbles
  Budget/         The character budget and its priority order
  Integration/    Conflict detection, and later other mods
  Settings/       Player settings
  Util/           Logging, text, JSON
docs/             Reference notes; per-pillar specs in docs/specs/
tools/            Headless harness
references/       Third-party code, read-only — not committed
1.6/Assemblies/   Build output
```

## Documentation

Four files, each the sole owner of its subject. Nothing is stated twice; they cite each other.

- **[DESIGN.md](DESIGN.md)** — what each feature is and how it is meant to work.
- **[PILLARS.md](PILLARS.md)** — the work cut into phases and pillars. Start here to pick up work.
- **[STATUS.md](STATUS.md)** — how far along each pillar is. Nothing else records progress.
- **[docs/RIMTALK-TEARDOWN.md](docs/RIMTALK-TEARDOWN.md)** — prior art. RimTalk solved many of
  these problems first and is worth learning from.

## Credits

RimTalk by **juicy** — Arkh does not use its code, but it charted this territory first, and the
teardown above records what was learned from it. The companion mods listed here solved these
problems before us too.
