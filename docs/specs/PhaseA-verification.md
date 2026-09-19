# Phase A — verification checklist

One load, worked end to end. Phase A (P1–P4) is the whole engine: prompt, model client, talk
scheduling, display.

**Stage 0 costs nothing — do all of it first.** Most of what could be wrong is visible from the
main menu, and every fault found there is one the twenty-minute load does not have to spend itself
on. Only start Stage 1 once Stage 0 is clean.

---

## Stage 0 — no colony loaded

### 0.1 Before RimWorld

```bash
./build.ps1
./tools/smoke-test.ps1
```

- [ ] Build: 0 warnings, 0 errors
- [ ] Harness: all checks pass

A `MissingMethodException` here is a BCL overload that exists in the reference assemblies and not
in the game's runtime. It has happened twice, and in game it presents as a dead feature rather than
as an error — so a red harness means stop, not proceed.

### 0.2 Mod list

- [ ] Arkh appears, with no red dependency warning
- [ ] Harmony and Interaction Bubbles are both active and load **before** Arkh
- [ ] RimTalk is **not** active (they are declared incompatible and both would generate dialogue)

### 0.3 Mod Options → Arkh, still at the main menu

- [ ] The settings page opens and scrolls without errors in the log
- [ ] **Test connection** with provider **Mock** → green, and reports a parsed line
- [ ] **Test connection** with your real provider and key → green

That second one is the point of the button. It sends the real instruction and output contract and
reads the reply back through the real parser, so a green result means this provider *and this
model* will produce speech Arkh can use — not merely that the endpoint answered. Fix any red here
before loading anything.

Red results worth recognising:

| Says | Means |
|---|---|
| "not configured yet" | No key, or an empty base URL for Custom |
| "rejected the API key" | Wrong key, or wrong provider for that key |
| "no such model or endpoint" | Model name misspelled, or base URL missing `/v1` |
| "could not be reached … local server" | Ollama or LM Studio is not running |
| "replied, but nothing usable could be read" | Reached the model; it cannot follow the output contract. Usually too small a model — try a larger one |

### 0.4 Prompt profile panel, still at the main menu

Open it from the button in settings.

- [ ] Panel opens with no colony loaded
- [ ] **Profile tab** lists the slots in order, with `SystemInstruction`, `OutputContract`,
      `WorldContext` and `PawnContext` populated and `Memory` / `Conversation` / `Trigger` shown as
      empty (those are later pillars, not faults)
- [ ] Every age band's text reads the way you want it to. **Fix wording here, not in game** — this
      is exactly the sort of thing that would otherwise cost a second load
- [ ] Type some world lore; the character count updates and the text shows trimmed at its cap
- [ ] Budget section totals look sane, and nothing you care about says "squeezed"
- [ ] Drop the total budget to ~200: the output contract stays at full size, lore and age go to
      zero. A trimmed contract would lose every reply, so it is exempt by design

---

## Stage 1 — load a colony

### Setup

- A colony containing a **child, an adult and an elder** — the age bands must differ or they prove
  nothing
- Interaction Bubbles **enabled in its own settings**
- Provider **Mock**, so the first pass costs nothing
- Conversation interval down to ~10s, in-flight cap 2, so results arrive while you watch

### 1.1 Startup

- [ ] Log shows `[Arkh] ready — N prompt sections declared`
- [ ] No exceptions from Arkh during load
- [ ] No warning about Interaction Bubbles being inactive

### 1.2 It speaks

- [ ] Within a minute, a bubble appears above a colonist
- [ ] The same line appears in the social log (Ctrl+F11 log, or the interaction log)
- [ ] **Exactly one bubble per line** — two would mean something else is also rendering
- [ ] Panel → **Live tab**: request count climbing, failures at zero, tokens accumulating
- [ ] Recent lines list matches what you saw on screen

### 1.3 Who speaks

- [ ] Two colonists standing together produce an exchange naming both
- [ ] A colonist alone produces a monologue (with "Allow talking to oneself" on)
- [ ] Turn that setting off: a lone colonist stops, and the Live tab says *"nobody to talk to, and
      monologues are off"*
- [ ] Draft a colonist — they stop being chosen
- [ ] A sleeping colonist is not chosen
- [ ] Over a few minutes, several different colonists speak rather than one doing all of it

### 1.4 Age and lore actually land

- [ ] The child's lines read differently from the elder's
- [ ] With world lore set, lines reflect it
- [ ] Turn age voice off in settings: the next lines lose that character

### 1.5 Failure behaves

- [ ] Set **mock failure rate to 100%**: the Live tab shows failures rising and a readable reason,
      the log does **not** fill with exceptions, and the game does not stutter
- [ ] Set it back to 0: speech resumes without a restart
- [ ] Set in-flight cap to 1 and interval to 1s: in-flight never exceeds 1

### 1.6 Real provider

- [ ] Switch to your real provider; lines are recognisably better than the mock's canned ones
- [ ] Token counts in the Live tab move, and the numbers are plausible

### 1.7 Persistence

- [ ] Save, quit to menu, reload: no errors, counters reset, speech resumes
- [ ] Lines spoken before the save are still in the social log afterwards

---

## Not bugs — known placeholders

Do not spend the run investigating these. All are named in
[../../PILLARS.md](../../PILLARS.md) as later pillars.

- **Conversation range is a plain circle.** It ignores walls entirely. The real earshot model is P6.
- **Nobody remembers anything.** The `Memory` slot is empty until P7 and P8. This is the headline
  feature and it is not built yet.
- **No persona.** Colonists differ only by age, gender and whatever lore you wrote (P5).
- **Adults add no age line**, deliberately — they are the model's default register, so saying so
  would cost tokens on every prompt in the colony.
- **Gender voice stays silent** until you write both texts; it ships off with empty defaults.
- **A line may be attributed to the initiator** when the model used a name that matches nobody.
  Dropping it instead would waste a paid request.
- **No whisper, shout or thought yet** (P6).

## If something fails

Capture these three before changing anything — together they usually identify the cause without a
second load:

1. The Live tab's **"Not starting anything"** reason, verbatim.
2. The Live tab's **last failure**, including the detail line.
3. The log around `[Arkh]`, plus any exception stack.

Silence with no reason shown in the Live tab is the one genuinely surprising outcome — it would
mean the scheduler is not running at all, rather than declining to run.
