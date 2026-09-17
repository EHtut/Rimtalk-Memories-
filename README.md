# RimTalk Memories

An overhaul layer for [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752).
One mod in place of a shelf of companions.

Colonists remember. Memories are scored for weight and for how much they matter right now, and
the ones that belong together surface together — so a pawn recalls the raid and the person they
lost in it in the same breath, not as two disconnected lines.

Around that sits the talking itself: editable prompt presets, separate voices by age and gender,
shared world lore, dialogue templates, distance rules for who can hear what, length controls, and
whisper / shout / thought as distinct ways of speaking.

**Requires RimTalk and Harmony. RimWorld 1.6.**

> ⚠️ Early development. See [STATUS.md](STATUS.md) for what actually works today — currently the
> context foundation only, and it has not yet been run in-game.

## Mods this replaces

Run these alongside it and both will write into the same prompts, so pawns repeat themselves.
The mod logs a warning at startup if it finds any of them active; it will not disable them for
you.

| Mod | Package id | Superseded by |
|---|---|---|
| RimTalk Context Upgrade | `wuren.rimtalkcontextupgrade` | Context and prompt settings |
| RimTalk Event+ | `saltgin.rimtalkeventmemory` | Event memory |
| RimTalk Lucid Chronicle | `alus.rimtalk.lucidchronicle` | Long-term memory and world lore |
| RimTalk Distance Control | `youyu.rimtalk.distancecontrol` | Conversation distance |

Keep this table in step with `Superseded` in
[Source/Integration/CompanionConflicts.cs](Source/Integration/CompanionConflicts.cs).

RimTalk **Dialogue Patch** is *not* on this list. Despite the name it is a UI mod for RimTalk's
talk window, not a dialogue library, and there is nothing here that conflicts with it.

## Building

The project lives inside `RimWorld/Mods/`, so it builds straight into place — no copy step.

```bash
./build.ps1
```

Needs the .NET SDK. Game assemblies come from NuGet reference packages, so no RimWorld install is
required to *compile*; `RimTalk.dll` is located automatically in the Steam Workshop folder or in
`Mods/RimTalk/`. If it is somewhere else:

```bash
./build.ps1 -RimTalkDll "C:\path\to\RimTalk.dll"
```

`-Configuration Debug` for a debug build. Restart RimWorld to pick up a new assembly.

The compiled DLL **is** committed — players install this repo as the mod folder and most will not
have a .NET SDK.

## Layout

```
About/            Mod manifest
Source/
  Integration/    Everything that touches RimTalk lives behind this seam
  Context/        Context providers (age, gender, world lore)
  Settings/       Player settings
  Util/           Logging
docs/             Reference notes
references/       Third-party code, read-only — not committed, see references/README.md
1.6/Assemblies/   Build output
```

## Documentation

Four files, each the sole owner of its subject. Nothing is stated in two places; they cite each
other instead.

- **[DESIGN.md](DESIGN.md)** — what each feature is and how it is meant to work.
- **[PILLARS.md](PILLARS.md)** — the work cut into pieces: scope, order, dependencies, exit
  criteria. Start here to pick up work.
- **[STATUS.md](STATUS.md)** — how far along each pillar is. Nothing else records progress.
- **[docs/RIMTALK-API.md](docs/RIMTALK-API.md)** — RimTalk's extension surface, established by
  decompiling v1.2.14. Recheck it when RimTalk updates.

## Credits

RimTalk by **juicy**. This mod stands on the public extension API RimTalk provides, and on prior
art from the companion mods listed above — their authors solved these problems first.
