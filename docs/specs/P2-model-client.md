# P2 — Model client: working spec

Scope and exit criteria are in [../../PILLARS.md](../../PILLARS.md) P2. This is the *how*, and the
record of decisions already taken.

## Decisions

**Synchronous `Complete`, not `Task<Completion>`.** The talk engine dispatches on a worker thread
anyway ([../../DESIGN.md](../../DESIGN.md) §18), so async buys nothing here and costs the ability
to call a provider straight from the harness. A synchronous call on a thread we already own is
simpler to reason about and simpler to test.

**Failure is a value, not an exception.** `Complete` never throws. For this kind of integration
failure is an ordinary outcome — no key, rate limit, local server not running — and modelling it as
exceptional puts a try/catch in every caller and loses the distinction between kinds.

`FailureKind` separates them because each wants a different response: a bad key is permanent and
worth saying once; a rate limit clears by waiting; an unreachable localhost means Ollama is not
running, which is a completely different sentence from "your quota is gone". `Transient` says
whether retrying the identical request could plausibly work — retrying a bad key just burns quota
faster.

**`HttpWebRequest`, not `HttpClient` or `UnityWebRequest`.** It runs on a plain worker thread with
no Unity coupling and no async machinery, which is what lets `tools/smoke-test.ps1` drive the real
client exactly as the game does. A Unity-bound transport would only be testable inside a
twenty-minute load. TLS 1.2 is enabled per request because Mono's default can exclude it.

**Hand-rolled JSON** (`Source/Util/Json.cs`). RimWorld ships no Newtonsoft, and
`DataContractJsonSerializer` wants attributed types — a poor fit when every provider returns a
differently-shaped envelope. Accessors never throw and missing paths stay navigable, so reading a
field out of an unexpected shape yields an empty string rather than a `NullReferenceException`
three frames from the cause.

**The mock is a provider, not a test double.** It is the default on a fresh install, so the mod
does something visible before the player has found an API key. Its failure-rate dial exists because
code that has only ever seen success handles failure badly, and a colonist falling silent is the
one failure mode that looks like a bug in everything else.

## Built

- `IModelClient`, `ChatMessage`, `ModelOptions`, `Completion`, `ModelFailure`
- `MockClient` — seeded and reproducible, with delay and failure-rate dials
- `OpenAiCompatibleClient` — covers OpenAI, OpenRouter, DeepSeek, Together, LM Studio and Ollama
- `ModelProviders` — per-provider defaults and the factory
- `Json` — reader and writer
- Settings and settings UI, with the key hidden behind a Show toggle
- 22 harness checks over JSON, request shaping, response parsing and every failure branch

## Remaining

- [ ] **Gemini** — native `generateContent` shape. A second request/response format; everything
      else is shared.
- [ ] **Player2** — last, because its device-auth flow is the only one needing an interactive
      login, and that is UI work as much as client work.
- [ ] **A "test connection" button** in settings. Currently the only way to find out whether a key
      works is to start a colony, which is exactly the twenty-minute loop this project avoids
      everywhere else.
- [ ] **Cost surfacing.** `Completion` carries token counts; nothing shows them yet. A running
      total per session belongs in the profile panel.
- [ ] **Retry with backoff** for transient failures only, with a hard ceiling. `Transient` already
      marks which kinds qualify.

## Verification

`tools/smoke-test.ps1` covers everything except real network calls. What it cannot answer:

1. Whether a real provider accepts our request body. The harness proves the body is valid JSON with
   the right fields, not that OpenAI likes it.
2. Whether TLS negotiates inside Unity's Mono. This is the likeliest surprise, and the "test
   connection" button above is how it should be answered rather than by a colony load.

## Notes for whoever is next

Two runtime-only bugs have come from the same family, both caught by the harness and neither by the
compiler: `String.TrimEnd()` and `String.TrimEnd(char)` are .NET Core additions that exist in our
reference assemblies and not in the runtime the game uses. Passing **two or more** chars binds to
`params char[]`, which is safe. Assume any convenient-looking BCL overload added after .NET
Framework 4.8 is a trap, and let the harness find it.
