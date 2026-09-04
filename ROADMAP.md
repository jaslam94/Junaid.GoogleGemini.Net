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
- [x] **Full audit and live re-verification** (`6.4.1`): every transport, service, and options file
      read again against the shipped `6.4.0` source and Google's current docs/pricing/changelog
      (re-fetched, not assumed current), then every fix confirmed against a real, billing-enabled key.
      Six real bugs found and fixed, no breaking changes:
      1. `GenerateImageAsync()`'s default model had been broken since `6.1.0`. `RecommendedImage`
         (and `Gemini31FlashImage`/`Gemini3ProImage`) pointed at `-preview`-suffixed model IDs Google
         promoted to GA under unsuffixed names on May 28, 2026 and shut down on June 25. Fixed; added
         the previously-uncatalogued `Gemini31FlashLiteImage`. Live-verified with a raw REST call
         bypassing the library first, then the actual paid-tier test suite, including decoding real
         returned pixel dimensions.
      2. Cost-governance pricing for `gemini-3.6-flash` was stale by 2x: Google moved it onto
         `gemini-3.7-flash`'s introductory rate when 3.7-flash shipped August 13, two days before this
         audit. Corrected both entries after a cross-check itself surfaced a false lead (a different
         Google product's pricing page and a stale third-party listing both disagreed with the real
         rate); added `gemini-3.7-flash` as the new default; added real per-token pricing for the
         current image models, which turned out to be token-priced rather than per-image as assumed
         in `PLAN-cost-governance.md` §11.
      3. `GeminiRateLimiter` floored any `RequestsPerMinute` under 60 to an effective 60 RPM via
         integer division, exactly the range free/low-tier quotas live in. Fixed; the fix's own
         structure made a divide-by-zero/negative-`TimeSpan` crash newly reachable through the public
         constructor, so added explicit null/range guards too.
      4. `ISafetyService.IsContentSafe` only recognized safety-rating probability strings, not the
         `BLOCK_*` threshold vocabulary every sibling method on the same interface uses or produces,
         so the natural, consistent way to call it reported almost all content as unsafe. Both
         vocabularies are now recognized. Zero prior test coverage; added `SafetyServiceTests.cs`.
      5. Live testing surfaced a real field this library was silently discarding on every response:
         `promptTokensDetails`/`candidatesTokensDetails`, a per-modality token breakdown Google sends
         but `UsageMetadata` had no properties for. Added `ModalityTokenCount` and both properties;
         this is also the data a future pass would need to price `gemini-3-pro-image`'s and
         `gemini-3.1-flash-lite-image`'s mixed text/image output exactly instead of the current
         safe-but-approximate single-rate estimate (documented on `GeminiCostGovernor.DefaultPricing`).
      6. Assorted doc-comment fixes naming the previous default model after `Models.Recommended`
         moved, caught only in a deliberate second pass, not the first.

      Also confirmed still-open, not touched this pass: File Search (managed RAG), Google Maps
      grounding, TTS, Computer Use, multimodal embeddings input (the `gemini-embedding-2` model
      constant exists but `EmbedContentAsync` is still text-only), and Flex/Priority inference tiers.
      Full result: build 0 warnings/errors on all three targets, 143/143 unit tests, 36/36 live tests
      (main suite, paid-tier-gated image generation and context caching, and a real Batch API job
      polled to completion).

      **Cost-governance competitive note**: since April 1, 2026, Google itself enforces mandatory
      account-wide monthly spend caps (not configurable, not per-project, no daily granularity, blocks
      every request account-wide when hit, no in-app observability). This sharpens rather than weakens
      the differentiation case: this library remains the only one of ten surveyed .NET Gemini clients,
      including Google's own official SDK, offering a configurable daily ceiling, an optional
      per-request estimate, and a live `gemini.client.cost.usd` OpenTelemetry metric enforced
      in-process before a wasted call goes out. See `docs/articles/cost-governance.md`.
- [x] **Model refresh and a real GenerationConfig bug** (`6.4.2`): triggered by Google's September 2,
      2026 GA of `gemini-3.8-flash` ("our most intelligent Flash model, engineered for long-horizon
      software engineering, autonomous agents, and complex enterprise workflows"). Added as a model
      constant and promoted to `Models.Recommended`/`Defaults.Model` (`gemini-3.7-flash` now marked
      superseded-but-supported); same introductory `DefaultPricing` rate as 3.7-flash
      ($0.75/$3.75/$0.075 per 1M through 2026-12-31).

      A deliberate second, harder-verification pass while researching this caught two things a
      first pass missed:
      1. A `WebSearch`-summarized claim said Gemini 2.5 Pro/Flash/Flash-Lite "shut down October 16,
         2026." That turned out to be wrong. The official
         [deprecations page](https://ai.google.dev/gemini-api/docs/deprecations) literally reads "No
         shutdown date announced" for all three, confirmed by quoting the table cells verbatim
         instead of trusting a summarized answer. What is actually happening, confirmed via a Google
         staff forum reply (Pooja Kapse, July 30, 2026): Google is gating **new** API keys and
         projects off the 2.5 line ("limiting access to the 2.5 models to users who have actively
         used them in the past. These models are not deprecated"), while existing active users are
         unaffected. This repo's own live-test infrastructure already knew this
         (`LiveTestInfrastructure.cs` moved off `gemini-2.5-flash` on 2026-08-10 for exactly this
         reason), but it had never reached the user-facing docs and samples. `README.md`,
         `docs/articles/getting-started.md`, `docs/articles/extensions-ai.md`,
         `docs/articles/files-and-caching.md`, the ASP.NET Core sample, and the .NET Framework smoke
         sample all still used `gemini-2.5-flash`/`gemini-2.5-pro` as their copy-paste example. A
         first-time reader could hit a 404 that had nothing to do with this library. All refreshed to
         `gemini-3.8-flash`.
      2. A separate, third-party claim that `GenerationConfig.FrequencyPenalty`/`PresencePenalty`/
         `CandidateCount` now hard-error on Gemini 3.x (rather than being silently ignored, like
         `Temperature`/`TopP`/`TopK`) was only half right. Live-verified directly against
         `gemini-3.8-flash` with a real, billing-enabled key: `presencePenalty` and `frequencyPenalty`
         each return `HTTP 400 INVALID_ARGUMENT` ("Penalty is not enabled for this model");
         `candidateCount: 1` returns `200` normally. Documented the real behavior on both penalty
         properties; left `CandidateCount` alone since it already works as advertised.

      Verification: `dotnet build` 0 warnings/errors on all three targets; 144/144 unit tests; the
      `presencePenalty`/`frequencyPenalty`/`candidateCount` behavior confirmed via raw REST calls
      against `gemini-3.8-flash` (bypassing this library's own types, same verification style as the
      Batch API and 6.4.1 audits), not inferred from any secondary source.
- [x] **Full live re-verification against a confirmed billing-enabled key, and a real CI publishing
      outage found and fixed twice** (`6.4.3`, same day as `6.4.2`): after `6.4.2` shipped, ran the
      actual `Junaid.GoogleGemini.Net.IntegrationTests` suite (not the unit suite) against a real,
      confirmed-billing-enabled key, closing every gap the day's earlier work had left open.

      **The `v6.4.2` tag push itself failed to publish.** `ci.yml`'s `Publish to NuGet` job read
      `steps.nuget-login.outputs.api-key`, but `NuGet/login`'s real output is `NUGET_API_KEY`.
      Confirmed against the action's own `action.yml` on both `v1.1.0` and current `v1.2.0`: it was
      never called `api-key`. This had never been exercised live before. Trusted Publishing
      (commit `181d01d`) landed a few hours *after* `v6.4.1`'s tag was pushed the same day, so
      `v6.4.2` was the first real tag push to ever hit this code path. Fixed the output name, pinned
      `NuGet/login` to `v1.2.0` instead of the floating `v1`, and retagged `v6.4.2` onto the fix. This
      time, the actual CI log was checked before calling it published: `201 Created` for both the
      `.nupkg` and `.snupkg`, not just a green checkmark.

      **The first fix for the underlying gap was itself broken, and running it caught that.** To stop
      a future silent break in this path from going unnoticed until the next real release, a
      standalone `nuget-trust-check.yml` was added to dry-run the login step every month. Dispatching
      it immediately failed with a 401. `nuget.org`'s Trusted Publishing policy for this repo is
      scoped to the exact workflow **filename** (`ci.yml`), so a separate file fails this check by
      construction, no matter how healthy the real publish path is. It would have filed a false
      "failure" issue every month, forever. It already had, as issue #53, closed with an explanation.
      The fix moved the same dry-run logic into `ci.yml`'s own `publish` job, gated by an
      `IS_DRY_RUN` env var: `true` for `schedule` or `workflow_dispatch`, `false` for an actual tag
      push. That way it runs under the one workflow filename the policy trusts. This time it was
      verified live before merging. `ci.yml` was already a registered workflow, so
      `gh workflow run ci.yml --ref <branch>` could dispatch the fix directly. That run confirmed
      `IS_DRY_RUN: true`, confirmed `Push to NuGet` did not run at all (not just skipped-and-logged),
      and confirmed the verification step printed a real, non-empty key length. It was re-verified
      again from `master` after merge: the real tag-push path correctly skips `Publish to NuGet` on a
      plain branch push, and the dry-run path passed on `workflow_dispatch` from master.

      **Live test results, against a confirmed billing-enabled key.** Tier 1, with real spend visible
      in the AI Studio dashboard. The first attempt had wrongly assumed the key was free-tier, based
      on self-skipped tests. The real cause was a `GeminiPaidTier=1` env var this session had simply
      never set, not the key's actual status. Once that was corrected, all 36 tests in
      `Junaid.GoogleGemini.Net.IntegrationTests` passed: the 28 free-tier-safe tests, the 3
      image-generation tests, context caching, and the full Batch API suite, including a real job
      polled to completion in `1m 28s`. The `.NET Framework` smoke sample was also run live (a real
      .NET Framework 4.8 process, a real API call, `'pong'` back). The `6.4.2` docs sweep had fixed
      that sample's code but never actually run it.

      **A second doc-comment sweep caught two more "gemini-3.7-flash is the recommended model"
      claims** that the first `6.4.2` pass had missed, in `GeminiRequestOptions.Factual()`'s and
      `.Code()`'s XML docs. These ship in the package's IntelliSense, so a customer hovering over
      either method saw a name that was wrong the moment `6.4.2` shipped. Fixed those, plus the same
      models list in `GenerationConfig.Temperature` and `ContentRequestBuilder.WithTemperature` for
      consistency. Both of those already said "and later," so they were not actually wrong, just less
      explicit than the rest of the codebase.

      No functional changes in this entry. `6.4.3` is a same-day doc and CI-only follow-up to
      `6.4.2`, not a new feature.

---

## Success metrics

- 0 build warnings; meaningful test coverage on transport + serialization.
- Retry/streaming/cancellation provably correct (tested).
- `IChatClient` adapter published.
- README leads with the differentiation matrix.
- Download trajectory: 5k to 50k+ over time (resilience + DX + OTel as the hook).
