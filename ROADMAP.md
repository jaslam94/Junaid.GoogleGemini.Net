# Junaid.GoogleGemini.Net — Modernization Roadmap (v6)

> **Positioning:** The Gemini client for .NET that is **production-ready out of the box** —
> resilient, observable, DI-native, and ecosystem-friendly. We cover the modern Gemini API
> as table stakes; we *win* on developer experience, resilience, and observability — the one
> thing the official SDK and the big community wrappers all lack.

This document is the living plan. Each phase is independently shippable. We do **plan → review →
execute** one phase at a time. Every architectural decision below includes the *why*, so this
doubles as a "how to build a good .NET library" reference.

---

## Guiding principles (the "why" behind everything)

1. **Layer the design: transport → resources → ergonomics.**
   - *Transport* (`GeminiHttpClient`): auth, retry/backoff, rate limiting, telemetry, serialization. One place, done right.
   - *Resource clients* (`Models`, `Files`, `Caching`, `Embeddings`, `Content`): faithful 1:1 mappings to API endpoints, for power users.
   - *Ergonomic facade* (`IGeminiService`) + adapters (`IChatClient`): the easy 90% path.
   - **Why:** beginners stay high-level; advanced users drop down without forking. The current code mixes all three layers, which is why a streaming tweak risks breaking auth.

2. **One serialization strategy, source-generated.**
   - All models PascalCase, single `JsonSerializerContext` with `JsonNamingPolicy.CamelCase`.
   - **Why:** the current mix (some models use `[JsonPropertyName]`, others rely on hand-matched casing) is a silent-failure landmine. Source-gen is faster, trim-safe, and AOT-ready.

3. **Don't reinvent platform primitives.**
   - Resilience via `Microsoft.Extensions.Http.Resilience` (the modern, Polly-v8-based standard handler), not a hand-cached `AsyncRetryPolicy`. Drop the **deprecated** `Polly.Extensions.Http`.
   - **Why:** the hand-rolled retry currently reuses `HttpContent` across attempts → throws on every retry. The standard handler builds each attempt correctly and is battle-tested.

4. **Resilience belongs on the pipeline; requests are built per-attempt.**
   - **Why:** `HttpClient` disposes content after send. Anything inside a retry must be freshly constructed. This is the root cause of the current broken retry.

5. **Streaming is `IAsyncEnumerable`, over real SSE.**
   - Use `streamGenerateContent?alt=sse` + `System.Net.ServerSentEvents`. Keep a callback overload for convenience.
   - **Why:** the current line-sniffing parser drops function calls/thoughts/usage and breaks on whitespace changes.

6. **Errors are typed, never magic strings.**
   - `GeminiException` (base) → `GeminiApiException` (status/code/details), `GeminiRateLimitException`, `GeminiTimeoutException`.
   - **Why:** `Text()` returning `"[Content was blocked...]"` is unrecoverable, untranslatable, and indistinguishable from real output.

7. **Observability is first-class and dependency-free.**
   - `ActivitySource` + `Meter` from `System.Diagnostics` following the OpenTelemetry **GenAI semantic conventions**. No hard OTel dependency — consumers opt in.
   - **Why:** this is our headline differentiator. None of the giants ship traces/metrics for token usage, latency, retries.

8. **A test suite is not optional.**
   - xUnit + a `FakeHttpMessageHandler` with recorded JSON fixtures; CI gates every PR.
   - **Why:** zero tests is exactly why the v5 retry/streaming bugs shipped.

---

## Packaging strategy

- Keep the existing **package ID** `Junaid.GoogleGemini.Net` — renaming would orphan ~5k installs and lose search history. Improve Title/Description/Tags/icon/README instead.
- Split into:
  - `Junaid.GoogleGemini.Net` — core client, minimal deps.
  - `Junaid.GoogleGemini.Net.Extensions.AI` — `IChatClient`/`IEmbeddingGenerator` adapters (depends on `Microsoft.Extensions.AI.Abstractions`).
  - **Why:** keep optional/heavy deps out of the core; mirrors the ecosystem convention every competitor follows.
- Multi-target `net8.0;net9.0;netstandard2.0`.
  - **Why:** `netstandard2.0` reaches .NET Framework / Unity / older runtimes — a reach the official SDK has and we currently don't.

---

## Phase 0 — Foundation & safety net ✅ DONE *(no user-visible behavior change)*

Goal: make the repo a professional, testable, CI-gated project before touching logic.

- [ ] `Directory.Build.props`: shared props, `LangVersion`, `Nullable`, deterministic build, SourceLink, `EmbedUntrackedSources`, symbol packages (`.snupkg`), `ContinuousIntegrationBuild`.
- [ ] Multi-target `net8.0;net9.0;netstandard2.0`.
- [ ] Add **test project** (`tests/…Tests`, xUnit) + `FakeHttpMessageHandler` + JSON fixtures.
- [ ] Add **CI** (`.github/workflows/ci.yml`): restore/build/test on PR; pack + push on tag.
- [ ] Package metadata: icon, `PackageReadmeFile`, richer description/tags, `GeneratePackageOnBuild` for Release.
- [ ] Turn on analyzers; baseline the 32 nullable warnings (fix in Phase 1).

**Ships as:** internal milestone (no NuGet release needed).

---

## Phase 1 — Fix the broken core ✅ DONE *(version bumped to 6.0.0-alpha.1)*

Goal: everything advertised today actually works. Completed in 6 commits; 14 tests on net8.0+net9.0.
Note: these changes are breaking, so the line was moved from "5.2.0" to the **6.0** major.

- [ ] Rewrite transport: per-attempt request + content construction; adopt `AddStandardResilienceHandler`.
- [ ] Proper SSE streaming → `IAsyncEnumerable<GeminiResponseChunk>` (+ callback overload).
- [ ] Propagate `CancellationToken` to every network + rate-limiter call.
- [ ] Token-aware rate limiting **or** remove the false `TokensPerMinute` claim and document the real behavior.
- [ ] Single source-generated `JsonSerializerContext`; one casing policy; **fix all 32 nullable warnings**.
- [ ] Typed exception hierarchy; remove magic-string `Text()`, add safe accessors (`TryGetText`, `FinishReason`, `Usage`).
- [ ] Default base URL → `v1beta/` (configurable); drop deprecated model defaults (`gemini-1.5-pro`); refresh `Models` constants and add `Models.ListAsync()` for runtime discovery.
- [ ] Delete the empty `Models/ImageGeneration/` phantom folder.
- [ ] Tests for: retry-on-429/503, streaming parse, cancellation, error mapping, serialization round-trips.

**Ships as:** `5.2.0` — "it works now."

---

## Phase 2 — Modern API parity (table stakes) *(→ 6.0.0-preview)*

- [ ] System instructions.
- [ ] **Structured output**: `responseMimeType` + `responseSchema`, plus generic `GenerateAsync<T>(...)` that auto-derives the schema from a C# type and deserializes. (Flagship DX feature.)
- [ ] Thinking config (`thinkingBudget` / `thinkingLevel`, `includeThoughts`); surface `thoughtsTokenCount`.
- [ ] Embeddings: `taskType`, `outputDimensionality`, batch.
- [ ] Tools: `google_search` grounding, `url_context`, `code_execution`; expose `groundingMetadata`.
- [ ] Files API (resumable upload + ACTIVE-state polling).
- [ ] Context caching (`cachedContents` CRUD + use in requests).

**Ships as:** `6.0.0-preview` — modern feature set.

---

## Phase 3 — The differentiators (the wedge) *(→ 6.0.0)*

- [ ] `Junaid.GoogleGemini.Net.Extensions.AI`: `IChatClient` + `IEmbeddingGenerator` adapters (ecosystem interop: Semantic Kernel, agent frameworks).
- [ ] OpenTelemetry-native tracing + metrics (GenAI semconv): spans per request, token-usage/latency/retry counters.
- [ ] Polished, documented resilience + rate-limiting story; sensible production defaults.
- [ ] First-class ASP.NET Core minimal-API sample; DocFX/docs site; refreshed README with the "why choose this" matrix.

**Ships as:** `6.0.0` — the production-grade DX release.

---

## Phase 4 — Stretch / unique (optional)

- [ ] Image generation (`responseModalities`, Nano Banana models).
- [ ] Live API (bidirectional WebSocket) as a separate `*.Live` package.
- [ ] Batch API.

---

## Success metrics

- 0 build warnings; meaningful test coverage on transport + serialization.
- Retry/streaming/cancellation provably correct (tested).
- `IChatClient` adapter published.
- README leads with the differentiation matrix.
- Download trajectory: 5k → 50k+ over time (resilience + DX + OTel as the hook).
