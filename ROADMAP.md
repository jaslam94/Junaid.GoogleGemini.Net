# Junaid.GoogleGemini.Net: Modernization Roadmap (v6)

> Positioning: a Gemini client for .NET that works in production without extra setup. It has
> built-in resilience, observability, and DI support, and it fits the wider .NET ecosystem. We treat
> the modern Gemini API as the minimum. We aim to stand out on developer experience, resilience, and
> observability. The official SDK and the big community wrappers all lack these.

This document is the living plan. Each phase can ship on its own. We plan, review, and execute one
phase at a time. Every architectural decision below includes the reason for it, so the document
also works as a reference for how to build a good .NET library.

---

## Guiding principles

1. Layer the design: transport, then resources, then ergonomics.
   - Transport (`GeminiHttpClient`): auth, retry and backoff, rate limiting, telemetry, and
     serialization. One place, done right.
   - Resource clients (`Models`, `Files`, `Caching`, `Embeddings`, `Content`): faithful 1:1 mappings
     to API endpoints, for power users.
   - Ergonomic facade (`IGeminiService`) and adapters (`IChatClient`): the easy path that covers
     90% of use.
   - Why: beginners stay high-level, and advanced users can drop down without forking. The current
     code mixes all three layers, so a streaming tweak risks breaking auth.

2. Use one serialization strategy, and source-generate it.
   - All models use PascalCase. One `JsonSerializerContext` uses `JsonNamingPolicy.CamelCase`.
   - Why: the current mix is a silent-failure risk. Some models use `[JsonPropertyName]`, and others
     rely on hand-matched casing. Source generation is faster, trim-safe, and AOT-ready.

3. Do not reinvent platform primitives.
   - Use `Microsoft.Extensions.Http.Resilience` for resilience. It is the modern standard handler,
     built on Polly v8. Do not hand-cache an `AsyncRetryPolicy`. Drop the deprecated
     `Polly.Extensions.Http`.
   - Why: the hand-rolled retry reuses `HttpContent` across attempts, so it throws on every retry.
     The standard handler builds each attempt correctly and is well tested.

4. Put resilience on the pipeline, and build requests per attempt.
   - Why: `HttpClient` disposes content after a send. Anything inside a retry must be freshly built.
     This is the root cause of the current broken retry.

5. Make streaming an `IAsyncEnumerable` over real SSE.
   - Use `streamGenerateContent?alt=sse` and `System.Net.ServerSentEvents`. Keep a callback overload
     for convenience.
   - Why: the current line-sniffing parser drops function calls, thoughts, and usage. It also breaks
     when whitespace changes.

6. Type the errors. Do not use magic strings.
   - `GeminiException` is the base type. `GeminiApiException` (status, code, details),
     `GeminiRateLimitException`, and `GeminiTimeoutException` derive from it.
   - Why: `Text()` returning `"[Content was blocked...]"` cannot be recovered from or translated, and
     it looks the same as real output.

7. Make observability first-class and dependency-free.
   - Use `ActivitySource` and `Meter` from `System.Diagnostics`, following the OpenTelemetry GenAI
     semantic conventions. The library takes no hard OTel dependency. Consumers opt in.
   - Why: this is our main differentiator. None of the large libraries ship traces or metrics for
     token usage, latency, or retries.

8. Treat a test suite as required.
   - Use xUnit and a `FakeHttpMessageHandler` with recorded JSON fixtures. CI gates every PR.
   - Why: the v5 retry and streaming bugs shipped because the project had zero tests.

---

## Packaging strategy

- Keep the existing package ID `Junaid.GoogleGemini.Net`. A rename would orphan about 5k installs and
  lose search history. Improve the title, description, tags, icon, and README instead.
- Split the code into two packages:
  - `Junaid.GoogleGemini.Net`: the core client, with minimal dependencies.
  - `Junaid.GoogleGemini.Net.Extensions.AI`: the `IChatClient` and `IEmbeddingGenerator` adapters.
    It depends on `Microsoft.Extensions.AI.Abstractions`.
  - Why: this keeps optional and heavy dependencies out of the core. It also follows the convention
    that every competitor follows.
- Multi-target `net8.0;net9.0;netstandard2.0`.
  - Why: `netstandard2.0` reaches .NET Framework, Unity, and older runtimes. The official SDK has
    that reach, and we did not.

---

## Phase 0: Foundation and safety net (DONE)

This phase has no user-visible behavior change.

Goal: make the repo a professional, testable, CI-gated project before touching logic.

- [ ] `Directory.Build.props`: shared props, `LangVersion`, `Nullable`, deterministic build,
      SourceLink, `EmbedUntrackedSources`, symbol packages (`.snupkg`), `ContinuousIntegrationBuild`.
- [ ] Multi-target `net8.0;net9.0;netstandard2.0`.
- [ ] Add a test project (`tests/<Project>.Tests`, xUnit), a `FakeHttpMessageHandler`, and JSON
      fixtures.
- [ ] Add CI (`.github/workflows/ci.yml`): restore, build, and test on PR. Pack and push on tag.
- [ ] Package metadata: icon, `PackageReadmeFile`, richer description and tags,
      `GeneratePackageOnBuild` for Release.
- [ ] Turn on analyzers. Baseline the 32 nullable warnings (fix them in Phase 1).

Ships as: an internal milestone. No NuGet release is needed.

---

## Phase 1: Fix the broken core (DONE)

Version bumped to 6.0.0-alpha.1.

Goal: everything advertised today actually works. The phase took 6 commits and has 14 tests on
net8.0 and net9.0. These changes are breaking, so the line moved from "5.2.0" to the 6.0 major.

- [ ] Rewrite the transport: per-attempt request and content construction. Adopt
      `AddStandardResilienceHandler`.
- [ ] Add proper SSE streaming, exposed as `IAsyncEnumerable<GeminiResponseChunk>` plus a callback
      overload.
- [ ] Propagate `CancellationToken` to every network call and rate-limiter call.
- [ ] Make rate limiting token-aware, or remove the false `TokensPerMinute` claim and document the
      real behavior.
- [ ] Use one source-generated `JsonSerializerContext` and one casing policy. Fix all 32 nullable
      warnings.
- [ ] Add a typed exception hierarchy. Remove the magic-string `Text()`. Add safe accessors
      (`TryGetText`, `FinishReason`, `Usage`).
- [ ] Set the default base URL to `v1beta/` (configurable). Drop deprecated model defaults
      (`gemini-1.5-pro`). Refresh the `Models` constants and add `Models.ListAsync()` for runtime
      discovery.
- [ ] Delete the empty `Models/ImageGeneration/` folder.
- [ ] Add tests for retry on 429 and 503, streaming parse, cancellation, error mapping, and
      serialization round-trips.

Ships as: `5.2.0`, "it works now."

---

## Phase 2: Modern API parity (DONE)

Version 6.0.0-alpha.2.

Delivered: system instructions. An expanded generation config (JSON mode, thinking, seed,
penalties). The flagship `GenerateAsync<T>` structured output. Tools (function declarations, Google
Search grounding, url_context, code_execution) and groundingMetadata. Embeddings (taskType,
dimensionality). The Files API (resumable upload and polling). Context caching (cachedContents CRUD
and request reuse). The phase has 26 tests on net8.0 and net9.0.

<details><summary>Original checklist</summary>

- [ ] System instructions.
- [ ] Structured output: `responseMimeType` and `responseSchema`, plus a generic
      `GenerateAsync<T>(...)` that derives the schema from a C# type and deserializes the reply.
      This is the flagship DX feature.
- [ ] Thinking config (`thinkingBudget` and `thinkingLevel`, `includeThoughts`). Surface
      `thoughtsTokenCount`.
- [ ] Embeddings: `taskType`, `outputDimensionality`, batch.
- [ ] Tools: `google_search` grounding, `url_context`, `code_execution`. Expose `groundingMetadata`.
- [ ] Files API (resumable upload and ACTIVE-state polling).
- [ ] Context caching (`cachedContents` CRUD and use in requests).

</details>

---

## Phase 3: The differentiators (DONE)

Version 6.0.0-alpha.3.

- [x] `Junaid.GoogleGemini.Net.Extensions.AI`: `IChatClient` and `IEmbeddingGenerator` adapters, for
      interop with Semantic Kernel and agent frameworks.
- [x] OpenTelemetry-native tracing and metrics (GenAI semconv): spans per request, and token-usage
      and latency histograms. The library has no OTel dependency.
- [x] Documented resilience and rate-limiting behavior, with sensible production defaults.
- [x] Refreshed README with the "why choose this" matrix and accurate 6.0 examples.
- [x] First-class ASP.NET Core minimal-API sample and a DocFX docs site.
- [x] `netstandard2.0` target with polyfills (alpha.4): PolySharp, Microsoft.Bcl.AsyncInterfaces, the
      System.Text.Json package, and a hand-written `GeminiRetryHandler` where
      `Microsoft.Extensions.Http.Resilience` (net8+ only) is not available.

Ships as: `6.0.0`, the release with the resilience, observability, and adapter work.

---

## Phase 4: Stretch and unique features (optional)

- [x] Image generation (`6.1.0`): `GenerateImageAsync` (`responseModalities`, Nano Banana models
      `gemini-3.1-flash-image-preview` and `gemini-3-pro-image-preview`). The `Images()`,
      `TryGetImages()`, and `GetImagesOrThrow()` response accessors. `ImageAspectRatio` and
      `ImageSize` (`imageConfig`) for Gemini 3+ image models. We verified it live end to end. That
      included aspect ratio (we decoded the actual pixel dimensions to confirm the API does not
      silently ignore it) and image-only output. There is no streaming image generation yet.
- [x] Cost governance (`6.2.0`): `GeminiOptions.Budget` (`BudgetOptions`). It has a cumulative daily
      USD ceiling (`MaxCostPerDayUsd`, exact, built from real `UsageMetadata`). It also has an
      opt-in, best-effort per-request estimate ceiling (`MaxCostPerRequestUsd`). Both cover
      non-streaming and streaming calls. The library also records every priced call's cost as the
      `gemini.client.cost.usd` OpenTelemetry metric, whether or not enforcement is on. We surveyed
      nine other .NET Gemini client libraries (including Google's own official SDK) and found none
      that offer this. See `docs/articles/cost-governance.md`.

      Follow-up (`6.3.1`): `MaxCostPerRequestUsd` did not cover `ChatAsync(IList<Content>)` and
      `StreamChatAsync(IList<Content>)`. These are the raw multi-turn overloads for Gemini 3
      function calling and `thoughtSignature` replay. No token-counting endpoint could estimate a
      raw `Content` list. A new `CountTokensChatAsync(IList<Content>, options, ct)` overload closed
      the gap. We verified live that Google's `countTokens` endpoint accepts a `Content` list with a
      `functionCall`/`functionResponse` pair and an encrypted `thoughtSignature`, and returns no
      error.

      Follow-up: the ASP.NET Core sample shipped with cost governance in 6.2.0 but never showed it.
      `samples/Junaid.GoogleGemini.Net.AspNetCoreSample` now sets `Gemini:Budget` with low demo
      ceilings. `GET /generate` catches both `GeminiBudgetExceededException` and
      `GeminiRequestCostExceededException`. A new `GET /spend` endpoint exposes
      `ICostGovernor.GetTodaySpend()`.
- [x] Live-verification and completeness pass (`6.3.0`): we re-tested every major feature against the
      real Gemini API instead of mocks. This found two response-parsing gaps. The library silently
      dropped `code_execution`'s `executableCode` and `codeExecutionResult` parts, and `url_context`'s
      `urlContextMetadata` field, on deserialization, because no model properties existed for them.
      Both are now typed and verified live. We also closed the last telemetry gap. `StreamAsync` had
      no OpenTelemetry instrumentation (only `PostAsync` had a span). It now emits one, with the same
      start, error-status, duration, and token-usage-tag pattern as `PostAsync`.
- [ ] Live API (bidirectional WebSocket) as a separate `*.Live` package.
- [ ] Revisit Google's Interactions API (GA since June 2026) as the request and response shape for
      TTS, once it supports the Batch API. We evaluated it for `6.5.0` and deferred it.
      `generateContent` remains fully supported per Google's own docs, despite a "Legacy" label. See
      `PLAN-tts.md` §9. Note: `6.6.0` (speech-to-text) does use the Interactions API. The reason is
      different. It is the only way to reach `gemini-3.5-transcribe` at all. It is not a choice
      between two shapes for the same model. See `PLAN-stt.md` §2 for why that decision went the
      other way from TTS.
- [x] Batch API (`6.4.0`): `IBatchService`, a new resource client that follows the pattern of
      `IFileService` and `ICachingService`. It submits large volumes of `generateContent` requests
      asynchronously at Google's 50%-discounted batch rate. It covers:
      - create (inline, from an already-uploaded JSONL file, or `CreateFromRequestsFileAsync`, a
        convenience method that writes and uploads the JSONL for a caller with an in-memory list)
      - get, list, cancel, and delete
      - a `WaitUntilCompleteAsync` polling helper
      - `GetResultsAsync`, which handles both inline and file-based results, including JSONL parsing

      We also added `IFileService.DownloadFileAsync`. The library needs it to fetch file-based batch
      results, and it is useful for any uploaded file. `IBatchService` uses its own dedicated
      `HttpClient` (`GeminiHttpClients.Batches`) and not the shared `IGeminiClient`. `GeminiClient`
      always routes every call through the interactive rate limiter and `ICostGovernor.CheckBudget`.
      Neither applies to batch's separate quota pool and discounted pricing. See
      `docs/articles/batch-api.md` for details, including what is out of scope: cost governance and
      rate limiting integration, batch embeddings, and client-side limit enforcement.

      We verified the Batch API live end to end on 2026-08-15 against a real, paid-tier key. The
      first version had passed only unit tests. It shipped with two guesses that live testing proved
      wrong. It also had a third, more serious structural gap that docs research never surfaced:

      - State prefix: the API uses `BATCH_STATE_*` (`BATCH_STATE_PENDING`, `RUNNING`, `SUCCEEDED`,
        `CANCELLED`), not `JOB_STATE_*`. Google's REST reference was right. The guide's and
        cookbook's `JOB_STATE_*` examples describe the Python SDK's own naming and not the raw wire
        format.
      - File-based results field: the API uses `responsesFile`, not `fileName`. The initial guess
        came from the guide's worked example and the Python SDK sample. Both describe the SDK layer
        and not the wire format. We renamed `BatchJobDestination.FileName` to `ResponsesFile`. That
        rename would have broken any caller who depended on it. We caught it before release only
        because we used a live key.
      - The bigger find: Create, Get, and List responses are not the flat batch resource that we
        first modeled. Google wraps them in a long-running-operation envelope
        (`{ name, metadata, done, error | response }`). The real batch fields (`state`,
        `batchStats`, `output`, `displayName`, and others) sit under `metadata`. None of the three
        research sources (guide, REST reference, cookbook) showed this as the real Create, Get, and
        List response shape. One fetch mentioned an "Operation" concept, but it read as a side issue,
        and we filed it away as one. Against the real API, the flat `BatchJob` would have
        deserialized `State`, `BatchStats`, and `DisplayName` as permanently `null`. Then
        `WaitUntilCompleteAsync` would have looped until timeout on every real job, whether it
        succeeded or not. We fixed this with a new `BatchJobMetadata` type under
        `BatchJob.Metadata`. `BatchJob` itself exposes the original flat properties (`State`,
        `Output`, `BatchStats`, and others) as read-only passthroughs. The public `IBatchService`
        surface and every caller-facing code example did not have to change.
      - A fourth, smaller find: the counts in `batchStats` (`requestCount`, `successfulRequestCount`,
        and others) arrive as JSON strings (`"1"`) and not JSON numbers. This is standard
        protobuf-JSON behavior for `int64` fields. The docs research did not turn it into the
        requirement that the C# properties need `[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]`
        or they throw. We fixed it the same way.

      Verification method: we made raw REST calls that bypass this library's own types. That showed
      Google's actual wire format with no risk that the library's own mapping hid a mismatch. The
      calls covered the create, get, cancel, list, and delete shapes on both inline and file-mode
      jobs, and polled one of each to real completion (a trivial 1-request job completed in under
      two minutes both times). Then we ran the fixed `IBatchService` and `BatchService` code against
      the same live API and confirmed that it works end to end. We also added a permanent live test
      (`BatchLiveTests.CreateAsync_WaitUntilComplete_ReturnsRealGeneratedText`). It waits for a real
      job to complete and asserts on the real generated text and real usage metadata.
- [x] Full audit and live re-verification (`6.4.1`): we read every transport, service, and options
      file again against the shipped `6.4.0` source and Google's current docs, pricing, and
      changelog. We re-fetched them and did not assume they were current. Then we confirmed every
      fix against a real, billing-enabled key. We found and fixed six real bugs. There are no
      breaking changes.
      1. `GenerateImageAsync()`'s default model had been broken since `6.1.0`. `RecommendedImage`
         (and `Gemini31FlashImage` and `Gemini3ProImage`) pointed at `-preview` model IDs. Google
         promoted them to GA under unsuffixed names on May 28, 2026 and shut the old IDs down on
         June 25. We fixed this and added the `Gemini31FlashLiteImage` constant, which was not
         catalogued before. We verified it live with a raw REST call that bypassed the library, then
         with the actual paid-tier test suite, including decoding the real returned pixel
         dimensions.
      2. Cost-governance pricing for `gemini-3.6-flash` was stale by 2x. Google moved it onto
         `gemini-3.7-flash`'s introductory rate when 3.7-flash shipped on August 13, two days before
         this audit. We corrected both entries. A cross-check had surfaced a false lead first: a
         different Google product's pricing page and a stale third-party listing both disagreed with
         the real rate. We added `gemini-3.7-flash` as the new default. We also added real per-token
         pricing for the current image models. They turned out to be priced per token and not per
         image, as `PLAN-cost-governance.md` §11 had assumed.
      3. `GeminiRateLimiter` raised any `RequestsPerMinute` under 60 to an effective 60 RPM through
         integer division. Free and low-tier quotas sit in exactly that range. We fixed it. The
         fix's own structure made a divide-by-zero or negative-`TimeSpan` crash reachable through the
         public constructor, so we also added explicit null and range guards.
      4. `ISafetyService.IsContentSafe` recognized only safety-rating probability strings. It did not
         recognize the `BLOCK_*` threshold vocabulary that every sibling method on the same interface
         uses or produces. The natural, consistent way to call it reported almost all content as
         unsafe. It now recognizes both vocabularies. It had zero test coverage, so we added
         `SafetyServiceTests.cs`.
      5. Live testing found a real field that the library silently discarded on every response:
         `promptTokensDetails` and `candidatesTokensDetails`, a per-modality token breakdown. Google
         sends it, but `UsageMetadata` had no properties for it. We added `ModalityTokenCount` and
         both properties. A future pass also needs this data to price the mixed text and image output
         of `gemini-3-pro-image` and `gemini-3.1-flash-lite-image` exactly. Today it uses a safe but
         approximate single-rate estimate (documented on `GeminiCostGovernor.DefaultPricing`).
      6. We fixed assorted doc comments that named the previous default model after `Models.Recommended`
         moved. A deliberate second pass caught these. The first pass did not.

      We also confirmed that these items are still open, and we did not touch them in this pass:
      File Search (managed RAG), Google Maps grounding, TTS, Computer Use, multimodal embeddings input
      (the `gemini-embedding-2` model constant exists, but `EmbedContentAsync` is still text-only),
      and the Flex and Priority inference tiers. Full result: the build had 0 warnings and errors on
      all three targets. All 143 unit tests passed. All 36 live tests passed (the main suite, the
      paid-tier-gated image generation and context caching, and a real Batch API job polled to
      completion).

      Cost-governance competitive note: since April 1, 2026, Google itself enforces mandatory
      account-wide monthly spend caps. They are not configurable and not per-project. They have no
      daily granularity. When a cap is hit, it blocks every request account-wide. They have no in-app
      observability. This strengthens the differentiation case. This library remains the only one of
      ten surveyed .NET Gemini clients, including Google's own official SDK, that offers a
      configurable daily ceiling, an optional per-request estimate, and a live
      `gemini.client.cost.usd` OpenTelemetry metric, all enforced in-process before a wasted call
      goes out. See `docs/articles/cost-governance.md`.
- [x] Model refresh and a real GenerationConfig bug (`6.4.2`): Google made `gemini-3.8-flash` GA on
      September 2, 2026 ("our most intelligent Flash model, engineered for long-horizon software
      engineering, autonomous agents, and complex enterprise workflows"). We added it as a model
      constant and promoted it to `Models.Recommended` and `Defaults.Model`. `gemini-3.7-flash` is now
      marked superseded but supported. `DefaultPricing` uses the same introductory rate as
      3.7-flash ($0.75/$3.75/$0.075 per 1M through 2026-12-31).

      A deliberate second, harder verification pass during this research caught two things that the
      first pass missed:
      1. A `WebSearch` summary said Gemini 2.5 Pro, Flash, and Flash-Lite "shut down October 16,
         2026." That was wrong. The official
         [deprecations page](https://ai.google.dev/gemini-api/docs/deprecations) says "No shutdown
         date announced" for all three. We confirmed this by quoting the table cells word for word
         and did not trust a summarized answer. A Google staff forum reply (Pooja Kapse, July 30,
         2026) explains what is happening. Google is gating new API keys and projects off the 2.5
         line ("limiting access to the 2.5 models to users who have actively used them in the past.
         These models are not deprecated"). Existing active users are unaffected. This repo's own
         live-test infrastructure already knew this (`LiveTestInfrastructure.cs` moved off
         `gemini-2.5-flash` on 2026-08-10 for this reason). But the knowledge had never reached the
         user-facing docs and samples. `README.md`, `docs/articles/getting-started.md`,
         `docs/articles/extensions-ai.md`, `docs/articles/files-and-caching.md`, the ASP.NET Core
         sample, and the .NET Framework smoke sample all still used `gemini-2.5-flash` or
         `gemini-2.5-pro` as their copy-paste example. A first-time reader could hit a 404 that had
         nothing to do with this library. We refreshed all of them to `gemini-3.8-flash`.
      2. A separate third-party claim said `GenerationConfig.FrequencyPenalty`, `PresencePenalty`,
         and `CandidateCount` now hard-error on Gemini 3.x. It said they no longer get silently
         ignored, the way `Temperature`, `TopP`, and `TopK` do. The claim was only half right. We
         verified it live against `gemini-3.8-flash` with a real, billing-enabled key.
         `presencePenalty` and `frequencyPenalty` each return `HTTP 400 INVALID_ARGUMENT` ("Penalty is
         not enabled for this model"). `candidateCount: 1` returns `200` normally. We documented the
         real behavior on both penalty properties. We left `CandidateCount` alone, because it already
         works as advertised.

      Verification: `dotnet build` had 0 warnings and errors on all three targets, and all 144 unit
      tests passed. We confirmed the `presencePenalty`, `frequencyPenalty`, and `candidateCount`
      behavior with raw REST calls against `gemini-3.8-flash`. These calls bypass this library's own
      types, the same verification style as the Batch API and 6.4.1 audits. We did not infer it from
      any secondary source.
- [x] Full live re-verification against a confirmed billing-enabled key, and a real CI publishing
      outage found and fixed twice (`6.4.3`, the same day as `6.4.2`): after `6.4.2` shipped, we ran
      the actual `Junaid.GoogleGemini.Net.IntegrationTests` suite (not the unit suite) against a
      real, confirmed-billing-enabled key. This closed every gap that the day's earlier work had left
      open.

      The `v6.4.2` tag push itself failed to publish. The `Publish to NuGet` job in `ci.yml` read
      `steps.nuget-login.outputs.api-key`, but the real output of `NuGet/login` is `NUGET_API_KEY`.
      We confirmed this against the action's own `action.yml` on both `v1.1.0` and the current
      `v1.2.0`. It was never called `api-key`. Nobody had exercised this path live before. Trusted
      Publishing (commit `181d01d`) landed a few hours after `v6.4.1`'s tag was pushed the same day.
      So `v6.4.2` was the first real tag push to hit this code path. We fixed the output name, pinned
      `NuGet/login` to `v1.2.0` and not the floating `v1`, and retagged `v6.4.2` onto the fix. This
      time we checked the actual CI log before we called it published. It showed `201 Created` for
      both the `.nupkg` and the `.snupkg`. A green checkmark alone was not enough.

      The first fix for the underlying gap was itself broken, and running it caught that. We wanted to
      stop a future silent break in this path from going unnoticed until the next real release. So
      we added a standalone `nuget-trust-check.yml` that dry-runs the login step every month. When we
      dispatched it, it failed at once with a 401. The Trusted Publishing policy of `nuget.org` for
      this repo is scoped to the exact workflow filename (`ci.yml`). A separate file fails this check
      by construction, however healthy the real publish path is. It would have filed a false failure
      issue every month, forever. It had already done so once, as issue #53, which we closed with an
      explanation. The fix moved the same dry-run logic into the `publish` job of `ci.yml`, gated by
      an `IS_DRY_RUN` env var. The value is `true` for `schedule` or `workflow_dispatch`, and `false`
      for an actual tag push. This way the check runs under the one workflow filename that the policy
      trusts. This time we verified it live before merging. `ci.yml` was already a registered
      workflow, so `gh workflow run ci.yml --ref <branch>` could dispatch the fix directly. That run
      confirmed `IS_DRY_RUN: true`. It confirmed that `Push to NuGet` did not run at all (it was not
      merely skipped and logged). It also confirmed that the verification step printed a real,
      non-empty key length. We verified it again from `master` after the merge. The real tag-push
      path correctly skips `Publish to NuGet` on a plain branch push, and the dry-run path passed on
      `workflow_dispatch` from master.

      Live test results, against a confirmed billing-enabled key: Tier 1, with real spend visible in
      the AI Studio dashboard. The first attempt wrongly assumed that the key was free-tier, because
      tests skipped themselves. The real cause was a `GeminiPaidTier=1` env var that this session had
      never set. It was not the key's actual status. After we corrected that, all 36 tests in
      `Junaid.GoogleGemini.Net.IntegrationTests` passed. That includes the 28 free-tier-safe tests,
      the 3 image-generation tests, context caching, and the full Batch API suite, with a real job
      polled to completion in `1m 28s`. We also ran the .NET Framework smoke sample live: a real
      .NET Framework 4.8 process made a real API call and got `'pong'` back. The `6.4.2` docs sweep
      had fixed that sample's code but had never run it.

      A second doc-comment sweep caught two more "gemini-3.7-flash is the recommended model" claims
      that the first `6.4.2` pass had missed. They were in the XML docs of `GeminiRequestOptions.Factual()`
      and `.Code()`. These ship in the package's IntelliSense, so a customer who hovered over either
      method saw a name that was wrong the moment `6.4.2` shipped. We fixed those. We also fixed the
      same models list in `GenerationConfig.Temperature` and `ContentRequestBuilder.WithTemperature`
      for consistency. Both already said "and later," so they were not wrong, only less explicit than
      the rest of the codebase.

      This entry has no functional changes. `6.4.3` is a same-day follow-up to `6.4.2` that changes
      only docs and CI. It is not a new feature.
- [x] Repo-wide writing style cleanup (`6.4.4`): adds `CLAUDE.md`, a standing rule for this repo. All
      documentation and releases use ASD STE-100 style, a human tone, and no em dashes. It also adds
      `RELEASE-RUNBOOK.md`, a step-by-step checklist for cutting a release. We wrote it directly from
      the two CI failures of the `6.4.2` and `6.4.3` cycle. Its most important step is to check the
      actual NuGet push log for a real `Created` line and not trust a green checkmark.

      We applied the style rule everywhere, in stages. We checked each stage with the user before we
      went further. First came `README.md` and `ROADMAP.md`. Then came every em dash in the C#
      codebase (130 spots across 50 files: the core library, both companion packages, samples,
      benchmarks, and all four test projects). Then came the two `PLAN-*.md` historical design
      documents (133 more). Then came a second, broader pass with no file-extension restriction. It
      caught `Directory.Build.props` (the first pass's narrower glob patterns had missed it) and an
      uncommitted local fix to `.gitignore`. We also applied the rule to the two published GitHub
      Releases for `6.4.2` and `6.4.3`.

      Fixing over 260 spots by hand risks breaking a sentence when the continuation of a multi-line
      comment lands on a line that the edit did not touch. That happened three times
      (`GeminiHttpClients.cs`, `GeminiOptions.cs`, and `integration.yml`). We caught it only by
      re-reading every edited region afterward, and not by assuming that the edit was correct. We
      verified the work with:
      - a full solution rebuild (0 warnings, so no XML doc comment was left malformed)
      - the unit suite (144/144)
      - `Testing.Tests` (6/6)
      - a clean `NetFrameworkSmoke` build and live run
      - the complete live `IntegrationTests` suite against a real, billing-enabled key (36/36). This
        included an isolated re-confirmation of all 3 `BatchLiveTests`. The captured log of the first
        full run was truncated and did not show them, even though its own summary line already said
        `Total: 36, Passed: 36`.

      This entry has no functional changes anywhere. It changes comments, doc comments, and prose
      only.
- [x] Text-to-speech (TTS) (`6.5.0`): new `GenerateAudioAsync` and `StreamAudioAsync` on
      `IGeminiService`. They use the same `responseModalities` pattern that `GenerateImageAsync`
      already set for images. Both single speaker (`VoiceName`) and multi speaker (`SpeakerVoices`, a
      named-speaker script) work. New `GenerateContentResponse.Audio()`, `TryGetAudio()`, and
      `GetAudioOrThrow()` return a `GeneratedAudio` with a `ToWav()` helper. Gemini's raw PCM output
      is not a playable file on its own.

      We designed it the way we designed `PLAN-batch-api.md`. We wrote a full plan document
      (`PLAN-tts.md`) before any code, and verified it live against the real API first. A first web
      search described a different, newer "Interactions API" shape (`input`, `response_format`,
      `interaction.output_audio.data`). It does not match this library's `generateContent`
      architecture at all. We discarded that shape before we wrote any code. We made raw REST calls
      instead, and they confirmed the real `generationConfig.speechConfig` shape that this library
      uses. Those live calls also pinned down the exact response `mimeType`
      (`"audio/L16;codec=pcm;rate=24000"`), which `ToWav()` depends on to build a correct WAV header.
      They also confirmed that audio output tokens already arrive tagged `AUDIO` in the existing
      per-modality `UsageMetadata` breakdown (added in `6.4.1`). Cost governance therefore needed only
      new pricing data for the three TTS models, and no new plumbing.

      The library does not list Google's 30 named voices as C# constants. This matches its existing
      policy of not allow-listing model names. A caller passes a plain string, and the API rejects a
      typo.

      Verified: the full solution build had 0 warnings on all three targets. We wrote 12 unit tests
      (request shape, response decoding, and byte-level checks of the `ToWav()` WAV header against
      known input, for both real mimeType shapes below). We also ran 5 live TTS tests against a real
      key, and all passed. We re-ran the full 41-test live suite (paid tier included) fresh on
      2026-09-06 against a newly confirmed billing-enabled key. We took TTS model pricing and
      free-tier availability from a single fetch of Google's pricing page, and not from a live
      billing check. Re-verify both before you rely on them for a cost-sensitive or free-tier
      deployment (see `PLAN-tts.md` §2).

      We also checked whether this should use Google's newer "Interactions API" instead. It should
      not. That surface lacks Batch API support (a feature this library already ships). It has no
      official .NET SDK to build against. Google states that `generateContent` "remains fully
      supported" despite the "Legacy" label. `PLAN-tts.md` §9 records the full reasoning and a
      revisit trigger, and we track it above.

      Someone asked us directly whether we had tested this properly. Our first answer was no.
      The PR description understated real gaps. We had called only 1 of the 3 model constants. We had
      checked the `ToWav()` output only for a valid-looking header, and never for real audio content.
      We had never run the new sample endpoint. We closed those gaps live right after the question,
      and found a real bug. `gemini-3.1-flash-tts-preview` returns a different response `mimeType`
      than the 2.5-era models: `"audio/l16; rate=24000; channels=1"`. It is lowercase and spaced, it
      has no `codec=pcm`, and it has an explicit channel count. The old form was
      `"audio/L16;codec=pcm;rate=24000"`. We had built the first `ToWav()` from only the first model
      we tested. It threw `FormatException` on this model's real response the first time we called
      it. We fixed it to parse the sample rate and channel count independently, wherever they appear,
      and not to match one rigid whole-string shape (see `PLAN-tts.md` §2's 2026-09-06 update). We
      now live-test all three model constants. The audio check reads the actual PCM samples for real
      amplitude variance, and no longer trusts a header that merely parses.

      Separately, during verification, the API key's account hit Google's mandatory monthly spend cap
      (`RESOURCE_EXHAUSTED`). It blocked every model, including the ones that had worked all day. All
      live testing stopped until the user raised the cap. This is a real operational fact about
      developing against this API at any volume. It is not a bug in this library.

      We started the `/speak` endpoint that we added to the ASP.NET Core sample and called it with a
      real request. Compiling it was not enough. The Unix `file` command identified its output
      independently as `RIFF ... WAVE audio, Microsoft PCM, 16 bit, mono 24000 Hz`. That is a
      confirmation from a tool that knows nothing about this library's own assumptions.
- [x] Speech-to-text (STT) (`6.6.0`): new `ITranscriptionService` (`TranscribeAsync`,
      `TranscribeFileAsync`, `StreamTranscribeAsync`, `GetInteractionAsync`). `AddGemini` registers it
      alongside every other service. It wraps Gemini's dedicated `gemini-3.5-transcribe` model, which
      offers speaker diarization, word-level timestamps, and custom vocabulary biasing. It mirrors the
      TTS work of `6.5.0` with the same rigor. We wrote a full plan document (`PLAN-stt.md`) before
      any code. We verified it live against the real API first, and again after implementation
      through the actual typed client.

      Unlike every other feature in this library, `generateContent` cannot reach the dedicated
      transcription model. It lives behind a different endpoint, the Interactions API
      (`POST /v1beta/interactions`). That endpoint has its own request shape, response shape, error
      shape, and SSE streaming protocol (named events such as `step.delta` and
      `interaction.completed`, and not one response-shaped chunk per line). A plain chat model can
      still transcribe audio through ordinary `generateContent` (confirmed live, see `PLAN-stt.md`
      §3.1). But that path has no diarization or timestamps, which are the real differentiators of the
      dedicated model. So this library built the Interactions path. This is the opposite choice from
      the TTS of `6.5.0`, which avoided the Interactions API on purpose. See `PLAN-stt.md` §2 for why
      the same question ("should we use the newer API?") got a different answer for STT.

      We added a new `InteractionRequest`, `Interaction`, `InteractionUsage`, and
      `InteractionsApiErrorResponse` model family. We also added new `IGeminiClient.PostInteractionAsync`,
      `GetInteractionAsync`, and `StreamInteractionAsync` methods. We did not reuse the existing
      `generateContent` client path. Reusing it unmodified would have caused a real bug. The error
      body of the Interactions API has a string `code` (for example `"invalid_request"`), where the
      existing error type expects a numeric one. A real 400 would have come back as an opaque
      `GeminiSerializationException` and not as the API's actual message. `GeminiApiException` gained
      a new `InteractionErrorCode` property to carry it.

      We also caught this live, before shipping. The API's own `usage.total_output_tokens` field reads
      `0` in every one of nine separate live calls that we made while researching this feature. That
      includes calls that clearly produced real output text. (`usage.total_tokens` also excludes
      output entirely.) The real output token count sits in
      `usage.model_invocation_token_counts[].candidates_tokens_details`. `GeminiClient` maps
      `InteractionUsage` into the existing `UsageMetadata` shape and reads from the correct field. The
      existing cost-governance and OpenTelemetry machinery then works unchanged. If we had read the
      obvious top-level field, the library would have priced every transcription's output at $0
      forever. We also confirmed live that real diarization speaker labels are `"spk:0"` and
      `"spk:1"` (zero-indexed, colon-separated). They are not the `"spk_1"` format that some docs
      describe.

      The real API rejects `CustomVocabulary` (`HTTP 400`) when combined with `DiarizationMode` or
      `TimestampGranularities`. `TranscriptionOptions.Validate()` catches this client-side before any
      network call. A live test confirms that the client-side guard matches the real API's rejection
      exactly. We did not assume it from the client-side check alone.

      Verified: the full solution build had 0 warnings across all three targets. We wrote 11 new unit
      tests (request shape, `Interaction.Text()` and `Words()` step filtering, the error-shape
      regression, and the output-token usage-mapping quirk above). The full 167-test unit suite is
      green. We wrote 5 new live tests against a real key (inline transcription, diarization with
      real distinct speaker labels, file-based transcription through the existing Files API,
      streaming, and the live rejection test above). The full live suite (39 passed, 7 paid-tier
      skipped) is green, which confirms no regression to any existing feature.

      We did not test long audio (Google's docs claim up to an hour per request). Every live call in
      this session used a clip a few seconds long and returned `"completed"` synchronously. We do not
      know whether long audio behaves differently (an `"in_progress"` status that needs a poll loop).
      This is an open item for whoever picks it up next. See `PLAN-stt.md` §7. We took pricing
      (`$2.00`/`$12.00` per 1M tokens) from a single fetch of Google's pricing page, and not from a
      live billing check. Re-verify it before you rely on it for a cost-sensitive deployment. The same
      standing caveat applies to every other pricing entry.

      We then asked for a deep second-pass review, done separately from the implementation pass above.
      That pass had already reported a fully green build and test suite. The review found and fixed
      three real bugs that none of the first pass's own tests caught. This is the reason to ask for a
      second pass by name, and not to treat "tests pass" as the finish line:
      1. `StreamInteractionAsync` changed the caller's own `InteractionRequest` object in place
         (`request.Stream = true` set directly on it). The library's own established pattern requires
         a clone, as in "on a cloned options object" for `GenerateAudioAsync` and
         `StreamAudioAsync`. A caller who reused one request object for both a streaming and a
         non-streaming call would have silently had `"stream": true` leak into the non-streaming one.
      2. `StreamTranscribeAsync` was not a real `async` iterator (it had no `yield return`). Its input
         validation threw synchronously at call time and not on the first `MoveNextAsync()`. Every
         other `IAsyncEnumerable` method in this codebase behaves the second way. A caller who wrapped
         only the `await foreach` in try/catch, the natural pattern given how every sibling method
         behaves, would have seen the exception escape uncaught.
      3. The `TotalTokenCount` of the usage-mapping helper omitted thought tokens. Real
         `generateContent` semantics are prompt + candidates + thoughts, confirmed live this session:
         8 + 2 + 96 = 106. Nothing reads `TotalTokenCount` directly today, so there was no observable
         effect. We fixed it for correctness. We also added a code comment that flags a gap: the
         cached-token subset assumption borrowed from `generateContent` was never live-exercised
         (every observed `TotalCachedTokens` was `0`).

      The review also found real gaps that were not bugs. No unit coverage existed for the SSE
      streaming path before this pass (only a live test exercised it). The ASP.NET Core sample never
      got a `/transcribe` endpoint, unlike TTS's `/speak`, even though its own checklist required it
      to be "actually run and hit with a real request, not just compiled." We fixed both. We added 5
      new unit tests that target exactly the three bugs above (172 total, up from 167). We added a new
      `GET /transcribe` sample endpoint that round-trips through TTS and STT together. We ran it
      against a live key, and `curl` returned the correct transcript with `HTTP 200`. Compiling it was not
      enough. The full solution build still had 0 warnings on all three targets after the fixes.
      See `PLAN-stt.md` §10 for the complete account.
- [x] Multimodal embeddings input (`6.7.0`): `gemini-embedding-2` has accepted image, video, audio,
      and PDF input since GA (April 2026). It maps all of them into one shared vector space alongside
      text. But `IEmbeddingService` was still text-only, a gap flagged in this file since the August
      2026 audit. We closed it with the same rigor as TTS and STT. We wrote
      `PLAN-embeddings-multimodal.md` and verified it live before any code, then verified it live
      again after implementation through the actual typed client.

      New `EmbedContentAsync(model, mediaBytes, mimeType, text)` handles inline media.
      `EmbedFileAsync(model, fileUri, mimeType, text)` reuses the existing Files API unchanged. A
      `BatchEmbedContentAsync` overload takes a new `EmbeddingInput` list, so a batch can mix
      text-only and multimodal entries. This is the smallest of the three additions in this session.
      It adds no new endpoint, no new model types, and no new client method. The `Content` field of
      `EmbedContentRequest` was already the same `Content` and `Part` type used everywhere else in
      this library (`inlineData`, `fileData`). The only gap was that `EmbeddingService` always built
      a text-only `Part`.

      We confirmed live that the image, audio, PDF, file-based, and mixed-batch paths all work. That
      included a real cosine-similarity check. A text-only embedding and a text+image embedding of the
      same prompt scored `0.528`. That is not `1.0` and not near zero. It proves that the media
      changes the embedding. The API does not accept the media and then ignore it. We did not live-test
      video input. Three different public sources for a small real video file all failed to connect
      from this session's sandbox. A network restriction caused that, not doubt about the mechanism,
      which is identical to the confirmed image and audio path. We documented it as an explicit open
      item and did not assume it works.

      We also caught and fixed a real footgun before shipping. `gemini-embedding-2` silently accepts
      `EmbeddingOptions.TaskType` and gives it zero effect. We confirmed this live twice: with raw REST
      during research, and again through the actual `IEmbeddingService` after implementation. Both
      used an identical-request A/B test that returned byte-for-byte identical embeddings with and
      without the field set. This is not an error. The API silently ignores the field, which is worse
      for DX than a hard rejection would have been. `EmbeddingService` now logs a one-time warning
      (not per call) the first time someone uses this combination. The doc comment of
      `EmbeddingOptions.TaskType` also carries the finding.

      Verified: the full solution build had 0 warnings across all three targets. We wrote 13 new unit
      tests (request shape for every new overload, validation guards, and the one-time-warning
      behavior, including a test that proves older embedding models do not trigger the warning). The
      full 185-test unit suite is green. We wrote 6 new live tests against a real key (image with a
      cosine-similarity assertion, audio without text, file-based, a hand-built PDF, mixed-batch, and
      the `TaskType` no-effect confirmation through the typed client). The broader 43-passing non-Batch
      live suite confirms no regression to any existing feature. There are no breaking changes.
- [x] Gemini 3.8 TTS and the October 2026 model refresh (`6.8.0`): Google shuts down all three TTS
      models this library shipped on 2026-11-17, and `GenerateAudioAsync` used one of them by default.
      We wrote `PLAN-tts-3-8.md` and checked every difference live before any code. The 3.8 models are
      not a drop-in swap. We found four breaks:
      1. A unary call returns `audio/wav`, not raw PCM. `GeneratedAudio.ToWav()` threw on it.
      2. The model speaks the prompt word for word. The docs example `"Say cheerfully: ..."` was read
         aloud. We proved it: output tokens went from 46 to 83.
      3. The multi-speaker request from `6.5.0` returns HTTP 400. Google now wants one part per turn,
         each with `speechMetadata.speaker`.
      4. The three old models reject `speechMetadata` with HTTP 400, so one shape cannot serve both.

      We kept the public API stable, as the maintainer chose. `SpeakerVoices` takes the same script. On a
      3.8 model the library splits it at each `Name:` marker and drops any text before the first marker.
      The old models keep the old shape. A new `UseSpeechMetadata` option overrides the choice by model
      name. New options: `SpeechStyle`, `AudioFormat` and `SpeechLanguageCode`. We left out a sample-rate
      option. Google documents one, but the API ignored requests for 16000 and 8000 in a live test. A
      `VoiceName` that starts with `voice_` or `voicekey_` now goes in the custom-voice field. `ToWav()`
      returns WAV input unchanged.

      Cost governance is now date-aware. Google's introductory price for `gemini-3.6-flash`,
      `gemini-3.8-flash` and the 3.8 TTS models ends on 2026-12-31. The standard rate (double) starts on
      2027-01-01. `ModelPricing` has `NextPricing` and `NextPricingEffectiveFromUtc`. The built-in table
      carries the 2027 rates, so a budget does not silently undercount by half in January. We assume
      00:00 UTC, because Google gives no time zone. `gemini-3.5-flash` and `gemini-3.7-flash` are priced
      as the models Google routes them to. We assume that is how Google bills them. We have not confirmed
      it.

      Smaller changes: the Nano Banana 2.1 model and its 1:4, 4:1, 1:8 and 8:1 ratios; the `512` image
      size; call `Id` on function calls and responses; `Part.MediaProcessing`; `[Obsolete]` on the 3.5
      and 3.7 Flash constants and the three old TTS constants; corrected thinking-level docs.

      Live findings worth keeping, all from 2026-10-10: `gemini-3.8-flash` rejects `minimal` thinking
      with HTTP 400. `ThinkingBudget = 0` was accepted on 3.6 and 3.8 and usually gave no thought tokens.
      It is not a guarantee. One call in eight on 3.8 still thought for 67 tokens, and none of eight on
      3.6. Google does not document it. A history that ends with a model turn returns HTTP 400. 3.8
      returns the call `id`, but a reply without it still worked. Nano Banana 2.1 rejects the `512` size.

      We then tested `Part.MediaProcessing` against two real clips of about 10 seconds from the
      maintainer's own files. The field works. Unset and `STATIC` behave the same (671 prompt tokens).
      `AGENTIC` answers correctly, but it returns `toolCall` and `toolResponse` parts that the library
      does not model (`Text()` still works). It also reports the video as `toolUsePromptTokenCount` (712
      to 806 tokens across runs) and leaves `promptTokenCount` at 11. The cost governor ignored that
      field, so it priced an agentic call at about a quarter of its real cost. We added
      `UsageMetadata.ToolUsePromptTokenCount` and price those tokens as input. That is an assumption.
      Google's pricing page bills tool tokens as input for URL context, and we found no per-model rate
      for video tool tokens. Any tool that loads input has the same gap. On these short clips `AGENTIC`
      used slightly more input tokens, not 88% fewer. Google's saving is for long videos, and we did not
      test long ones. The OpenTelemetry token metrics still report only `promptTokenCount`, so they
      under-report agentic calls. We did not change that.

      We then added live tests that pin every behavior this release documents from observation
      (`Gemini38BehaviorLiveTests` and `CustomVoiceLiveTests`, 12 tests). The 2 video tests need a local
      clip and show as skipped without one. Two of the new tests first failed, and both failures were
      wrong claims of ours, not library bugs. We had written that agentic mode needs a `mimeType` on a
      `fileData` part. An uploaded file works without one, and the error came from a YouTube URL. We had
      also written that `ThinkingBudget = 0` turns thinking off, and a full run caught a call that still
      thought. We corrected both claims in the docs, and the tests now assert what is true.

      We made a real custom voice with Google's voice design, used it with both 3.8 models, and deleted
      it. Voice design returned HTTP 400 for `gemini-3.8-flash-lite-tts`, although Google's page says both
      models support it, so the docs say to leave `voice.model` out. A custom voice costs very little to
      make (662 tokens). Two older live tests uploaded files and never deleted them, which left 2 files
      per run behind. They now delete them. `GenerateContentResponse.ModelVersion` is new, so a caller can
      see which model answered, and a live test uses it to pin the 3.5 and 3.7 routing.

      The live suite changed one decision. We planned to make Nano Banana 2.1 the default image model.
      Two live tests failed. The new model returned `finishReason: IMAGE_RECITATION` and no image for "a
      small red circle on a plain white background" (2 of 2 calls). A plain blue rectangle failed 1 of 6
      calls, and the same prompt worked on the next call. The old model returned an image. We reverted
      the default to `gemini-3.1-flash-image` and kept Nano Banana 2.1 as an opt-in constant. Google
      deprecated the old model but set no shutdown date. Revisit this when it does.

      A later round closed four gaps that the first pass had listed as "not verified".

      The `netstandard2.0` build had never been run, only compiled. We added a `net48` test target
      (Windows only, because Linux cannot host .NET Framework) and a Windows CI job. We ran the whole unit
      suite on .NET Framework 4.8. It found a real bug that exists only in that build.
      `StreamReader.ReadLineAsync` has no token overload on `netstandard2.0`, and the streaming helper
      ignored the token there. A caller who cancelled a stream on .NET Framework, Mono or Unity got no
      cancellation, and the cost governor recorded spend for the abandoned stream. The helper now honors
      the token. Two tests pin it. We proved that both fail on `net48` without the fix and pass on
      `net9.0` either way. The suite now has 271 tests on .NET Framework 4.8.

      The Voices API now has a wrapper, `IVoiceService`. It lists and filters the voice library, pages
      through it, gets a voice, designs one, replicates one, and deletes one. We read the real list,
      create and error shapes from live calls. We checked listing, every filter name, paging across
      several pages, and the 404 for a prebuilt voice against the real API. Unit tests cover the create
      call, built from the real response. Google's spending cap on the test project then blocked the live
      create test, so creating through the wrapper has not run live. The earlier test that creates a voice
      with a plain `HttpClient` did pass. Voice replication follows Google's documented shape only. It
      needs two recordings of a real speaker, including that speaker's consent. We did not run it, and we
      did not use anyone's voice or make a fake consent recording. An opt-in live test reads two
      recordings that the maintainer makes of their own voice. We have also not measured the long-video
      savings, because our only clips run about 10 seconds. An opt-in live test (`GeminiTestLongVideo`) is
      written.

      Verified: the full solution build had 0 warnings on all three targets. The unit suite has 273 tests
      on net8.0 and net9.0 and 271 on .NET Framework 4.8 (185 before). All are green. The last complete
      live run had 69 tests, including Batch and both video tests. 68 passed. The one failure was the
      thinking-budget test above. We corrected it, and it passed once. Google's spending cap on the test
      project then blocked every later billable call. So the corrected test has not had a second run, and
      the new live tests for the Voices wrapper ran only in their free parts (5 passed). Re-run the suite
      when the cap allows. The TTS live tests send their audio back through the transcription model, so
      they check what was said. A well-formed file alone does not pass. That proves the style direction
      is no longer spoken and that the preamble no longer leaks into the speech, for a unary call and for
      a stream.

      We did not verify: the long-video savings that Google claims for agentic video; voice replication;
      the designed-voice test through the new wrapper (blocked by the cap); or the `netstandard2.0` target
      on Mono or Unity (it runs on .NET Framework 4.8 only). The build machine has no .NET 8 runtime, so
      the `net8.0` tests ran on the .NET 9 runtime through roll-forward. There are no breaking changes to
      compiled code. The behavior changes are the default TTS model, `ToWav()` on WAV input, and prompts
      that put a direction in the text.

---

## Success metrics

- 0 build warnings. Meaningful test coverage on the transport and serialization.
- Retry, streaming, and cancellation are provably correct (tested).
- The `IChatClient` adapter is published.
- The README leads with the differentiation matrix.
- Download trajectory: 5k to 50k+ over time, with resilience, DX, and OTel as the hook.
