# Junaid.GoogleGemini.Net: Modernization Roadmap (v6)

> **Positioning:** The Gemini client for .NET that is **production-ready out of the box**:
> resilient, observable, DI-native, and ecosystem-friendly. We cover the modern Gemini API
> as table stakes, and we *win* on developer experience, resilience, and observability, the one
> thing the official SDK and the big community wrappers all lack.

This document is the living plan. Each phase is independently shippable. We do **plan, review,
execute** one phase at a time. Every architectural decision below includes the *why*, so this
doubles as a "how to build a good .NET library" reference.

---

## Guiding principles (the "why" behind everything)

1. **Layer the design: transport, then resources, then ergonomics.**
   - *Transport* (`GeminiHttpClient`): auth, retry/backoff, rate limiting, telemetry, serialization. One place, done right.
   - *Resource clients* (`Models`, `Files`, `Caching`, `Embeddings`, `Content`): faithful 1:1 mappings to API endpoints, for power users.
   - *Ergonomic facade* (`IGeminiService`) + adapters (`IChatClient`): the easy 90% path.
   - **Why:** beginners stay high-level; advanced users drop down without forking. The current code mixes all three layers, which is why a streaming tweak risks breaking auth.

2. **One serialization strategy, source-generated.**
   - All models PascalCase, single `JsonSerializerContext` with `JsonNamingPolicy.CamelCase`.
   - **Why:** the current mix (some models use `[JsonPropertyName]`, others rely on hand-matched casing) is a silent-failure landmine. Source-gen is faster, trim-safe, and AOT-ready.

3. **Don't reinvent platform primitives.**
   - Resilience via `Microsoft.Extensions.Http.Resilience` (the modern, Polly-v8-based standard handler), not a hand-cached `AsyncRetryPolicy`. Drop the **deprecated** `Polly.Extensions.Http`.
   - **Why:** the hand-rolled retry currently reuses `HttpContent` across attempts, so it throws on every retry. The standard handler builds each attempt correctly and is battle-tested.

4. **Resilience belongs on the pipeline; requests are built per-attempt.**
   - **Why:** `HttpClient` disposes content after send. Anything inside a retry must be freshly constructed. This is the root cause of the current broken retry.

5. **Streaming is `IAsyncEnumerable`, over real SSE.**
   - Use `streamGenerateContent?alt=sse` + `System.Net.ServerSentEvents`. Keep a callback overload for convenience.
   - **Why:** the current line-sniffing parser drops function calls/thoughts/usage and breaks on whitespace changes.

6. **Errors are typed, never magic strings.**
   - `GeminiException` (base), with `GeminiApiException` (status/code/details), `GeminiRateLimitException`, and `GeminiTimeoutException` beneath it.
   - **Why:** `Text()` returning `"[Content was blocked...]"` is unrecoverable, untranslatable, and indistinguishable from real output.

7. **Observability is first-class and dependency-free.**
   - `ActivitySource` + `Meter` from `System.Diagnostics` following the OpenTelemetry **GenAI semantic conventions**. No hard OTel dependency; consumers opt in.
   - **Why:** this is our headline differentiator. None of the giants ship traces/metrics for token usage, latency, retries.

8. **A test suite is not optional.**
   - xUnit + a `FakeHttpMessageHandler` with recorded JSON fixtures; CI gates every PR.
   - **Why:** zero tests is exactly why the v5 retry/streaming bugs shipped.

---

## Packaging strategy

- Keep the existing **package ID** `Junaid.GoogleGemini.Net`. Renaming would orphan ~5k installs and lose search history. Improve Title/Description/Tags/icon/README instead.
- Split into:
  - `Junaid.GoogleGemini.Net`: core client, minimal deps.
  - `Junaid.GoogleGemini.Net.Extensions.AI`: `IChatClient`/`IEmbeddingGenerator` adapters (depends on `Microsoft.Extensions.AI.Abstractions`).
  - **Why:** keep optional/heavy deps out of the core; mirrors the ecosystem convention every competitor follows.
- Multi-target `net8.0;net9.0;netstandard2.0`.
  - **Why:** `netstandard2.0` reaches .NET Framework, Unity, and older runtimes, a reach the official SDK has and we currently don't.

---

## Phase 0: Foundation & safety net (DONE) *(no user-visible behavior change)*

Goal: make the repo a professional, testable, CI-gated project before touching logic.

- [ ] `Directory.Build.props`: shared props, `LangVersion`, `Nullable`, deterministic build, SourceLink, `EmbedUntrackedSources`, symbol packages (`.snupkg`), `ContinuousIntegrationBuild`.
- [ ] Multi-target `net8.0;net9.0;netstandard2.0`.
- [ ] Add **test project** (`tests/<Project>.Tests`, xUnit) + `FakeHttpMessageHandler` + JSON fixtures.
- [ ] Add **CI** (`.github/workflows/ci.yml`): restore/build/test on PR; pack + push on tag.
- [ ] Package metadata: icon, `PackageReadmeFile`, richer description/tags, `GeneratePackageOnBuild` for Release.
- [ ] Turn on analyzers; baseline the 32 nullable warnings (fix in Phase 1).

**Ships as:** internal milestone (no NuGet release needed).

---

## Phase 1: Fix the broken core (DONE) *(version bumped to 6.0.0-alpha.1)*

Goal: everything advertised today actually works. Completed in 6 commits; 14 tests on net8.0+net9.0.
Note: these changes are breaking, so the line was moved from "5.2.0" to the **6.0** major.

- [ ] Rewrite transport: per-attempt request + content construction; adopt `AddStandardResilienceHandler`.
- [ ] Proper SSE streaming, exposed as `IAsyncEnumerable<GeminiResponseChunk>` (+ callback overload).
- [ ] Propagate `CancellationToken` to every network + rate-limiter call.
- [ ] Token-aware rate limiting **or** remove the false `TokensPerMinute` claim and document the real behavior.
- [ ] Single source-generated `JsonSerializerContext`; one casing policy; **fix all 32 nullable warnings**.
- [ ] Typed exception hierarchy; remove magic-string `Text()`, add safe accessors (`TryGetText`, `FinishReason`, `Usage`).
- [ ] Default base URL set to `v1beta/` (configurable); drop deprecated model defaults (`gemini-1.5-pro`); refresh `Models` constants and add `Models.ListAsync()` for runtime discovery.
- [ ] Delete the empty `Models/ImageGeneration/` phantom folder.
- [ ] Tests for: retry-on-429/503, streaming parse, cancellation, error mapping, serialization round-trips.

**Ships as:** `5.2.0`, "it works now."

---

## Phase 2: Modern API parity (table stakes) (DONE) *(6.0.0-alpha.2)*

Delivered: system instructions; expanded generation config (JSON mode, thinking, seed, penalties);
**flagship `GenerateAsync<T>` structured output**; tools (function declarations, Google Search
grounding, url_context, code_execution) + groundingMetadata; embeddings (taskType, dimensionality);
**Files API** (resumable upload + polling); **context caching** (cachedContents CRUD + request reuse).
26 tests on net8.0 + net9.0.

<details><summary>Original checklist</summary>

- [ ] System instructions.
- [ ] **Structured output**: `responseMimeType` + `responseSchema`, plus generic `GenerateAsync<T>(...)` that auto-derives the schema from a C# type and deserializes. (Flagship DX feature.)
- [ ] Thinking config (`thinkingBudget` / `thinkingLevel`, `includeThoughts`); surface `thoughtsTokenCount`.
- [ ] Embeddings: `taskType`, `outputDimensionality`, batch.
- [ ] Tools: `google_search` grounding, `url_context`, `code_execution`; expose `groundingMetadata`.
- [ ] Files API (resumable upload + ACTIVE-state polling).
- [ ] Context caching (`cachedContents` CRUD + use in requests).

</details>

---

## Phase 3: The differentiators (the wedge) *(6.0.0-alpha.3)*

- [x] `Junaid.GoogleGemini.Net.Extensions.AI`: `IChatClient` + `IEmbeddingGenerator` adapters (ecosystem interop: Semantic Kernel, agent frameworks).
- [x] OpenTelemetry-native tracing + metrics (GenAI semconv): spans per request, token-usage/latency histograms (no OTel dependency).
- [x] Documented resilience + rate-limiting story; sensible production defaults.
- [x] Refreshed README with the "why choose this" matrix and accurate 6.0 examples.
- [ ] First-class ASP.NET Core minimal-API sample; DocFX/docs site. *(still open)*
- [x] `netstandard2.0` target with polyfills (alpha.4): PolySharp + Microsoft.Bcl.AsyncInterfaces + System.Text.Json package + a hand-written GeminiRetryHandler where Microsoft.Extensions.Http.Resilience (net8+ only) isn't available.

**Ships as:** `6.0.0`, the production-grade DX release.

---

## Phase 4: Stretch / unique (optional)

- [x] **Image generation** (`6.1.0`): `GenerateImageAsync` (`responseModalities`, Nano Banana
      models `gemini-3.1-flash-image-preview`/`gemini-3-pro-image-preview`), `Images()`/
      `TryGetImages()`/`GetImagesOrThrow()` response accessors, `ImageAspectRatio`/`ImageSize`
      (`imageConfig`) for Gemini 3+ image models. Live-verified end-to-end including aspect ratio
      (decoded actual pixel dimensions to confirm it isn't silently ignored) and image-only output.
      No streaming image generation yet.
- [x] **Cost governance** (`6.2.0`): `GeminiOptions.Budget` (`BudgetOptions`), a cumulative daily
      USD ceiling (`MaxCostPerDayUsd`, exact, built from real `UsageMetadata`) plus an opt-in
      best-effort per-request estimate ceiling (`MaxCostPerRequestUsd`), both covering non-streaming
      and streaming calls. Every priced call's cost is also recorded as the `gemini.client.cost.usd`
      OpenTelemetry metric, whether or not enforcement is enabled. Surveyed nine other .NET Gemini
      client libraries (including Google's own official SDK) and found none offer this. See
      `docs/articles/cost-governance.md`. Follow-up (`6.3.1`): `MaxCostPerRequestUsd` didn't cover
      `ChatAsync(IList<Content>)`/`StreamChatAsync(IList<Content>)` (the raw multi-turn overload used
      for Gemini 3 function-calling/`thoughtSignature` replay), since there was no token-counting
      endpoint for a raw `Content` list to estimate against. A new
      `CountTokensChatAsync(IList<Content>, options, ct)` overload closed the gap; live-verified that
      Google's `countTokens` endpoint accepts a `Content` list carrying a
      `functionCall`/`functionResponse` pair and an encrypted `thoughtSignature` without erroring.
      Follow-up: the ASP.NET Core sample shipped cost governance features in 6.2.0 but never
      demonstrated them. `samples/Junaid.GoogleGemini.Net.AspNetCoreSample` now configures
      `Gemini:Budget` with tight demo ceilings, and `GET /generate` catches both
      `GeminiBudgetExceededException` and `GeminiRequestCostExceededException`; a new `GET /spend`
      endpoint exposes `ICostGovernor.GetTodaySpend()`.
- [x] **Live-verification and completeness pass** (`6.3.0`): every major feature re-tested against the
      real Gemini API rather than mocks, surfacing two response-parsing gaps. `code_execution`'s
      `executableCode`/`codeExecutionResult` parts and `url_context`'s `urlContextMetadata` field were
      silently dropped on deserialization, since no corresponding model properties existed; both are now
      typed and live-verified. Also closed the last telemetry gap: `StreamAsync` previously had zero
      OpenTelemetry instrumentation (only `PostAsync` had a span), and now emits one, matching
      `PostAsync`'s start/error-status/duration/token-usage-tag pattern.
- [ ] Live API (bidirectional WebSocket) as a separate `*.Live` package.
- [x] **Batch API** (`6.4.0`): `IBatchService`, a new resource client (mirroring `IFileService`/
      `ICachingService`'s pattern) for submitting large volumes of `generateContent` requests
      asynchronously at Google's 50%-discounted batch rate. Covers create (inline, from an
      already-uploaded JSONL file, or `CreateFromRequestsFileAsync`, a convenience method that writes
      and uploads the JSONL for a caller with an in-memory list), get, list, cancel, delete, a
      `WaitUntilCompleteAsync` polling helper, and `GetResultsAsync` (transparently handles both
      inline and file-based results, including JSONL parsing). Also added
      `IFileService.DownloadFileAsync`, needed to fetch file-based batch results but useful for any
      uploaded file. Deliberately uses its own dedicated `HttpClient`
      (`GeminiHttpClients.Batches`), not the shared `IGeminiClient`: `GeminiClient` unconditionally
      routes every call through the interactive rate limiter and `ICostGovernor.CheckBudget`, neither
      of which apply to batch's separate quota pool and discounted pricing. See
      `docs/articles/batch-api.md` for the full picture, including what's explicitly out of scope
      (cost governance and rate limiting integration, batch embeddings, client-side limit
      enforcement).

      **Live-verified end-to-end on 2026-08-15** against a real, paid-tier key, after an initial
      unit-tested-only version shipped with two guesses that live testing proved wrong, plus a third,
      more serious structural gap docs research never surfaced at all:

      - **State prefix**: confirmed `BATCH_STATE_*` (`BATCH_STATE_PENDING`/`RUNNING`/`SUCCEEDED`/
        `CANCELLED`), not `JOB_STATE_*`. Google's REST reference was right; the guide's and cookbook's
        `JOB_STATE_*` examples describe the Python SDK's own naming, not the raw wire format.
      - **File-based results field**: confirmed `responsesFile`, not `fileName` (the initial guess,
        based on the guide's worked example and the Python SDK sample, both of which turned out to
        describe the SDK layer, not the wire format). `BatchJobDestination.FileName` was renamed to
        `ResponsesFile` as a result - a real, breaking-if-anyone-had-depended-on-it fix caught before
        release only because a live key was actually used.
      - **The bigger find**: Create/Get/List responses aren't the flat batch resource this feature was
        originally modeled as. They're wrapped in a Google long-running-operation envelope
        (`{ name, metadata, done, error | response }`), with the real batch fields (`state`,
        `batchStats`, `output`, `displayName`, ...) nested under `metadata`. None of the three research
        sources (guide, REST reference, cookbook) surfaced this as the actual Create/Get/List response
        shape - one fetch mentioned an "Operation" concept but it read as tangential, not central, and
        was filed away as such. Against the real API, the original flat `BatchJob` would have
        deserialized `State`/`BatchStats`/`DisplayName` as permanently `null` - meaning
        `WaitUntilCompleteAsync` would have looped until timeout on every real job, succeeded or not.
        Fixed by nesting a new `BatchJobMetadata` type under `BatchJob.Metadata`, with `BatchJob`
        itself exposing the original flat properties (`State`, `Output`, `BatchStats`, ...) as
        read-only passthroughs, so the public `IBatchService` surface and every caller-facing code
        example didn't have to change.
      - **A fourth, smaller find**: `batchStats`' counts (`requestCount`, `successfulRequestCount`,
        etc.) arrive as JSON strings (`"1"`), not JSON numbers - standard protobuf-JSON behavior for
        `int64` fields, but not something the docs research had translated into "the C# properties need
        `[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]` or they'll throw." Fixed the
        same way.

      Verification method: raw REST calls (bypassing this library's own types entirely, to see Google's
      actual wire format without risk of the library's own mapping masking a mismatch) for the create/
      get/cancel/list/delete shapes on both inline and file-mode jobs, including polling one of each
      to real completion (a trivial 1-request job completed in under two minutes both times); then the
      actual `IBatchService`/`BatchService` code, fixed, run against the same live API and confirmed
      working end-to-end, including a new permanent live test
      (`BatchLiveTests.CreateAsync_WaitUntilComplete_ReturnsRealGeneratedText`) that waits for a real
      job to complete and asserts on the real generated text and real usage metadata.

---

## Success metrics

- 0 build warnings; meaningful test coverage on transport + serialization.
- Retry/streaming/cancellation provably correct (tested).
- `IChatClient` adapter published.
- README leads with the differentiation matrix.
- Download trajectory: 5k to 50k+ over time (resilience + DX + OTel as the hook).
