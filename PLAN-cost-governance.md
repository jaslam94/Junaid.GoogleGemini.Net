# Plan: Cost governance for Junaid.GoogleGemini.Net

Status: implemented and shipped in 6.2.0. We keep this file as a historical record. Code comments
cross-reference specific sections of it, for example the cost formula in §6.3 and the design
rationale in §4.

Audience: an AI coding agent (Cursor) that implements this directly against the current `master`
branch.

Author's confidence: we verified every file path, method signature, and integration point below by
reading the actual current source on 2026-08-02. We did not work from memory. We fetched the pricing
numbers from the live Google pricing page on the same date. Re-verify them before merging (see §7).

Post-implementation addendum: §2, §4, and §11 describe `MaxCostPerRequestUsd` as in scope, but they
leave its enforcement path unspecified. The `ICostGovernor` interface in §5.1 has no method for it.
No integration point, test, or checklist item covers how to wire it up. As a result, it shipped in
two stages. First, 6.2.0 landed everything specified here exactly as written. `MaxCostPerRequestUsd`
was a config-only property, with no enforcement. Then a follow-up change in the same 6.2.0 release
added the actual pre-flight enforcement. That change added `ICostGovernor.CheckEstimatedRequestCost`,
`ICostGovernor.HasRequestCeiling`, and `GeminiRequestCostExceededException`, and wired them into
`GeminiService`. It was a deliberate extension beyond the literal text of this plan. See
`docs/articles/cost-governance.md` for what actually shipped. That includes limitations that this
plan does not anticipate, for example no coverage for `ChatAsync(IList<Content>)`.

---

## 1. Goal

Let a caller cap what a Gemini integration can spend, and see what it actually spends, without
writing their own token-counting and pricing math. There are two mechanisms:

1. Cost observability (always on if the feature is enabled). The library converts every response's
   real token usage to a USD cost. It records the cost as an OpenTelemetry metric, using the exact
   pattern that the token and duration metrics already use.
2. Budget enforcement (opt-in). This is a configurable daily USD ceiling. When the cumulative
   actual spend for the day reaches the ceiling, the library rejects the next call before it sends
   it. The rejected call makes no network round-trip and costs nothing.

## 2. Explicit scope (read this before you write any code)

In scope:
- Only `GenerateContentResponse`-based calls. That means everything that already flows through
  `GeminiClient.PostAsync` and `StreamAsync` and fills `GenerateContentResponse.UsageMetadata`. This
  is exactly the set of calls that `GeminiTelemetry.RecordUsage` already instruments today.
- Both non-streaming (`PostAsync`) and streaming (`StreamAsync`) calls. Streaming must be included.
  A budget guardrail that silently does not apply to `StreamAsync` and `StreamChatAsync` would let a
  runaway streaming loop pass the budget unchecked. That defeats the purpose of the feature. See
  §5.3 for exactly what is missing there today.
- A cumulative daily budget, enforced from real recorded spend (exact, not estimated).
- An optional, opt-in per-request estimate (`MaxCostPerRequestUsd`). It is a best-effort guard
  only. See §4 for why it cannot be exact.

Out of scope for this change (do not implement):
- Embeddings (`EmbedContentAsync` and `BatchEmbedContentAsync`). We verified that
  `EmbedContentResponse` has no usage or token field today. There is nothing to cost without
  separate design work.
- The Files API and context caching operations themselves (the storage and management calls). The
  feature covers only the generation calls that reference a `CachedContent`. They are covered
  automatically, because they still return `GenerateContentResponse`.
- Per-minute or per-hour budgets. v1 supports daily budgets only (UTC calendar day).
- Multi-instance or distributed budget tracking (for example, a shared Redis-backed counter across
  horizontally scaled instances). The tracking is in-process, single-instance, and in-memory only.
  Document that limitation prominently (see §6.4).

## 3. Prerequisite bug fix (found during research, must land first)

`IGeminiService.CountTokensAsync(string prompt, CancellationToken ct)` has no way to specify a
model. Internally it always resolves to `Options.DefaultModel`
(`Junaid.GoogleGemini.Net/Services/GeminiService.cs`, `GetCountTokensEndpoint(null)`, which calls
`FormatModelName(null, Options?.DefaultModel)`). Suppose a caller does this:

```csharp
await gemini.GenerateAsync(prompt, new GeminiRequestOptions { Model = "gemini-3.1-pro-preview" });
```

This feature's per-request pre-flight estimate calls `CountTokensAsync(prompt)` to estimate input
cost. That call silently counts tokens, and therefore prices them, against the default model. It
does not use `gemini-3.1-pro-preview`, the model that the call will actually use. The pricing table
has materially different rates per model (see §7). So the estimate would be wrong for any call that
overrides `Model`.

The fix: add a new overload, and do not change the existing one:

```csharp
// IGeminiService.cs: new overload, existing 2-arg one stays exactly as-is
Task<CountTokensResponse> CountTokensAsync(
    string prompt,
    GeminiRequestOptions? options,
    CancellationToken cancellationToken = default);
```

Implement it in `GeminiService.cs` by threading `options?.Model` into `GetCountTokensEndpoint`. The
2-arg overload currently hardcodes `GetCountTokensEndpoint(null)`. Change that call site to delegate
to the new 3-arg overload with `options: null`. Have the new overload pass `options?.Model` through.
Mirror the same fix for `CountTokensWithImageAsync` and `CountTokensChatAsync` for consistency, even
though this feature needs only the plain-text one. Leaving them inconsistent would trip up the next
person. Add the 3 new overloads to `IGeminiService.cs` with XML docs. Keep the 3 existing overloads
and have them call through to the new ones with `options: null`.

This is a real bug fix that is worth shipping on its own. Call it out as such in the PR description,
separate from the cost-governance feature itself.

## 4. Why the daily budget comes first, and we do not count tokens before every call

We rejected the naive design: before every call, call `countTokens`, estimate the cost, compare it
to the budget, then send the real call. It doubles the number of API calls. It adds an extra HTTP
round-trip before every single generation call, forever, even when spend is nowhere near the budget.
That is a poor trade for a feature named "cost governance." The design is also structurally
incomplete. `countTokens` returns only `TotalTokens` for the input. It cannot know the output token
count in advance. A pre-flight check can bound only the input side precisely. The output side is
unknown until generation completes. `MaxTokens` bounds it only loosely, if the caller set one.

The design we used:
- The primary mechanism is always correct, and it is cumulative. After every real response, compute
  its actual cost from the real `UsageMetadata`. Add it to a running daily total, and record it
  through OTel. Before sending the next call, check whether the running total has already reached
  the configured ceiling. This check is cheap, in-memory, and makes no extra HTTP call. It is exact
  because it uses real billed tokens. It catches the "$4k overnight" runaway-loop scenario on the
  very next iteration. That is the actual failure mode this feature exists to prevent.
- The secondary, opt-in mechanism (`MaxCostPerRequestUsd`) is an estimate. Input cost is exact,
  through the fixed `CountTokensAsync` overload from §3. `MaxTokens` bounds the output cost if the
  caller set one. Otherwise, the design needs a configurable worst-case assumption. The default is to
  treat the output as unbounded and skip the output half of the estimate. In that case, the estimate
  bounds only the input cost unless `MaxTokens` is set. Document this precisely. Do not let the XML
  doc comment imply a guarantee that it cannot make.

## 5. Integration points (exact files, exact locations)

### 5.1 New interface and implementation, mirroring `IRateLimiter` and `GeminiRateLimiter` exactly

Read `Junaid.GoogleGemini.Net/Infrastructure/GeminiRateLimiter.cs` first. This new component must
follow the same shape: an interface, a real implementation, and a `CreateDisabled()` factory for the
zero-overhead-when-unused case. Inject it into the `GeminiClient` constructor in the same way as
`IRateLimiter`.

New file `Junaid.GoogleGemini.Net/Infrastructure/GeminiCostGovernor.cs`:

```csharp
namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>Tracks and enforces a cumulative daily USD budget for Gemini generation calls.</summary>
public interface ICostGovernor
{
    /// <summary>
    /// Checked BEFORE a call is sent. Throws <see cref="GeminiBudgetExceededException"/> if today's
    /// (UTC) cumulative spend has already reached the configured daily ceiling. Cheap: in-memory only,
    /// no network call.
    /// </summary>
    void CheckBudget();

    /// <summary>
    /// Called AFTER a successful response, with its real usage. Computes actual USD cost, adds it to
    /// today's running total, and records the <c>gemini.client.cost.usd</c> OTel metric (see §6.2).
    /// Returns the cost of this one call, in case the caller wants to log/expose it.
    /// </summary>
    decimal RecordSpend(string? model, UsageMetadata usage);

    /// <summary>Today's (UTC) cumulative recorded spend, for diagnostics/exposure.</summary>
    decimal GetTodaySpend();
}

public sealed class GeminiCostGovernor : ICostGovernor
{
    // Implementation notes:
    // - Store as ConcurrentDictionary<DateOnly, decimal> keyed by UTC calendar day (DateOnly, not
    //   DateTime, avoids any time-of-day/timezone ambiguity in the key itself). Use
    //   ConcurrentDictionary<DateOnly, decimal>.AddOrUpdate for thread-safe accumulation. Multiple
    //   concurrent calls from the same process must not race and undercount.
    // - Use `decimal` for ALL money math, never float/double. This is a financial calculation;
    //   floating-point error accumulating across thousands of calls is a correctness bug, not a
    //   cosmetic one.
    // - Prune old dictionary entries opportunistically (e.g. on RecordSpend, drop entries older than
    //   2 days) so a long-lived singleton doesn't grow unbounded over months of uptime.
    // - CreateDisabled() factory returns an implementation whose CheckBudget() is a no-op and whose
    //   RecordSpend() still computes+records the metric (observability stays on) but never throws.
    //   "Disabled" means "no enforcement," matching the existing IRateLimiter.CreateDisabled()
    //   semantics of "always succeeds" rather than "does nothing at all." Mirror that precedent.
}
```

### 5.2 `GeminiClient.PostAsync`: both the gate and the record point

File: `Junaid.GoogleGemini.Net/Infrastructure/GeminiClient.cs`.

Constructor: add `ICostGovernor costGovernor` as a fourth constructor parameter. Store it in a new
`_costGovernor` field. Use the same null-check pattern as the other three (`ArgumentNullException`
if null).

In `PostAsync<TRequest, TResponse>`, add the following immediately after the existing rate-limiter
gate (`using var lease = await _rateLimiter.AcquireAsync(...)`, around line 94-98) and before the
request is built:

```csharp
_costGovernor.CheckBudget();
```

This throws `GeminiBudgetExceededException` (a `GeminiException` subtype, see §6.1) before any HTTP
work happens. That mirrors how the `GeminiRateLimitException` of the rate limiter is thrown before
the request is built. No `try/catch` is needed here. The existing
`catch (GeminiException) { activity?.SetStatus(...); throw; }` block in `PostAsync` catches
`GeminiBudgetExceededException` (we verified that block is present at line ~120). The exception
propagates cleanly, and the generic `catch (Exception ex)` handler does not wrap it.

Then, in the existing block that already does telemetry recording:

```csharp
if (result is GenerateContentResponse contentResponse)
{
    GeminiTelemetry.RecordUsage(operation, model, contentResponse.Usage, contentResponse.FinishReason, activity);
}
```

add the following immediately after it:

```csharp
if (result is GenerateContentResponse { Usage: not null } usageResponse)
{
    _costGovernor.RecordSpend(model, usageResponse.Usage!);
}
```

We use two separate `is` checks on purpose, and we do not reuse `contentResponse` from above. This
keeps the patch minimal and additive, and does not restructure the existing conditional. The diff
stays easy to review.

### 5.3 `GeminiClient.StreamAsync`: the piece that is missing today

File: the same file, `StreamAsync<TRequest>`. Important finding: `StreamAsync` today has zero
telemetry instrumentation of any kind. It has no `GeminiTelemetry.StartOperation`, no `RecordUsage`,
and no `RecordDuration`. Only `PostAsync` is instrumented. This gap already existed. We
acknowledged it, and it is unrelated to this feature. But cost governance cannot silently inherit
it. If the library does not cost-track streaming calls, anyone who uses `StreamAsync` or
`StreamChatAsync` can bypass the daily budget ceiling. Both are documented, first-class APIs.

Required additions to `StreamAsync`:
1. Add the same `_costGovernor.CheckBudget()` call at the top, in the same place that the
   rate-limiter lease is acquired (right after `using var lease = await _rateLimiter.AcquireAsync(...)`).
2. Gemini's SSE stream carries `usageMetadata` on the final chunk. Intermediate chunks usually omit
   it or carry partial data. Track the most recent non-null `UsageMetadata` across the yielded
   chunks. Add a local `UsageMetadata? finalUsage = null;` before the `while (true)` loop. After
   each successful `yield return chunk;` (both the per-blank-line one and the trailing-flush one at
   the end of the method), do `finalUsage = chunk.UsageMetadata ?? finalUsage;`.
3. After the stream completes successfully (after the `while` loop and the trailing-flush block,
   inside the existing `try`, before the `finally`), call `_costGovernor.RecordSpend(model,
   finalUsage)` if `finalUsage is not null`. You need `model` in scope. `StreamAsync` currently
   does not call `GeminiTelemetry.Parse(endpoint)` at all. Add that at the top of the method (the
   same one-liner that `PostAsync` already uses), only to get `model` for this purpose. Do not add
   full duration or operation telemetry to `StreamAsync` as part of this change. That is the
   existing gap, and we track it separately. Stay within what cost governance needs: the model name
   and the final usage snapshot.
4. If the stream is cancelled or throws partway through, do not record spend for whatever partial
   usage you saw. Gemini bills for what the server actually generated, whether or not the client
   kept reading. But this library cannot know the true billed amount without the final
   `usageMetadata`. Recording a partial or guessed figure would be worse than recording nothing.
   Document this as a known limitation: a cancelled stream's actual cost will not appear in the
   tracked total. Do not silently guess.

### 5.4 DI registration

File: `Junaid.GoogleGemini.Net/Extensions/GeminiExtensions.cs`, inside `AddGemini(services,
configureOptions)`.

Add the following near the existing rate-limiter registration (`services.AddSingleton<IRateLimiter>(...)`):

```csharp
services.AddSingleton<ICostGovernor>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
    return new GeminiCostGovernor(options.Budget, GeminiTelemetry.RecordCost);
});
```

Cursor finalizes the exact constructor shape as an implementation detail. The requirements are a
singleton lifetime, the same as the rate limiter, and a build from `GeminiOptions.Budget`.

The code registers `GeminiClient` through `services.AddHttpClient<GeminiClient>(...)`. The
`GeminiClient` constructor now takes `ICostGovernor` as a fourth parameter. Standard constructor
injection picks it up automatically. No explicit wiring is needed beyond the `AddSingleton<ICostGovernor>`
line above.

## 6. New types

### 6.1 Exception

Add a new file `Junaid.GoogleGemini.Net/Exceptions/GeminiBudgetExceededException.cs`. Follow the
exact pattern of the existing `GeminiRateLimitException` in `GeminiExceptions.cs` (extra properties
beyond the message, same constructor style):

```csharp
namespace Junaid.GoogleGemini.Net.Exceptions
{
    /// <summary>
    /// Thrown by <see cref="ICostGovernor.CheckBudget"/> before a request is sent, when today's (UTC)
    /// cumulative spend has already reached the configured <see cref="BudgetOptions.MaxCostPerDayUsd"/>
    /// ceiling. The rejected call itself never reaches the network, so it costs nothing.
    /// </summary>
    public class GeminiBudgetExceededException : GeminiException
    {
        /// <summary>Today's (UTC) cumulative spend at the moment this call was rejected, in USD.</summary>
        public decimal CurrentSpendUsd { get; }

        /// <summary>The configured daily ceiling that was reached, in USD.</summary>
        public decimal BudgetLimitUsd { get; }

        public GeminiBudgetExceededException(string message, decimal currentSpendUsd, decimal budgetLimitUsd)
            : base(message)
        {
            CurrentSpendUsd = currentSpendUsd;
            BudgetLimitUsd = budgetLimitUsd;
        }
    }
}
```

Put it in `GeminiExceptions.cs` next to the others. This matches the existing file organization: all
typed exceptions other than `GeminiApiException` live in that one file. Use a new file only if
Cursor judges that the file is getting too large. The file is currently ~68 lines. One more class
keeps it well under any reasonable limit, so prefer the existing file.

### 6.2 Telemetry

File: `Junaid.GoogleGemini.Net/Infrastructure/Telemetry/GeminiTelemetry.cs`. Add the following next
to the existing `TokenUsage` and `OperationDuration` histograms:

```csharp
internal static readonly Counter<double> CostUsd = Meter.CreateCounter<double>(
    "gemini.client.cost.usd", unit: "USD", description: "Cumulative USD cost of Gemini client operations.");
```

Naming note. Do not get this wrong. The existing metrics (`gen_ai.client.operation.duration` and
`gen_ai.client.token.usage`) use the `gen_ai.*` prefix because those are stable, official
OpenTelemetry GenAI semantic convention attribute names. As of this plan's writing, the OTel GenAI
semconv spec has no official `gen_ai.client.cost.*` metric. Reusing that prefix would imply a
standards compliance that does not exist. Use `gemini.client.cost.usd` instead. It uses this
library's own namespace, which matches `GeminiTelemetry.SystemName = "gemini"`. If a future OTel
semconv version adds an official cost attribute, migrating is a deliberate follow-up. Do not guess
now. Before you implement, check the current OTel GenAI semconv spec in case this has changed. If an
official name now exists, use that instead and note the change in the PR description.

Use a `Counter<double>` and not a `Histogram<double>`. Cost is cumulative and additive by nature.
This matches how billing dashboards represent spend. The existing histograms model a distribution of
individual values (duration, or token count per call). Tag it in the same way that `RecordUsage` and
`RecordDuration` tag their metrics (`gen_ai.system`, `gen_ai.operation.name`,
`gen_ai.request.model`). This keeps it consistent, and it then correlates with the existing metrics
in any dashboard.

Add a method that mirrors the shape of `RecordUsage`:

```csharp
internal static void RecordCost(string? model, decimal costUsd)
{
    var tags = new TagList { { "gen_ai.system", SystemName } };
    if (model is not null) tags.Add("gen_ai.request.model", model);
    CostUsd.Record((double)costUsd, tags);
}
```

### 6.3 Configuration

File: `Junaid.GoogleGemini.Net/Infrastructure/Options/GeminiOptions.cs`. Add a new property and a
new nested options class. Follow the exact style of the existing `RateLimitOptions`:

```csharp
/// <summary>Cost governance settings. Null (the default) disables the feature entirely, with zero overhead.</summary>
public BudgetOptions? Budget { get; set; }
```

```csharp
/// <summary>Cost governance configuration.</summary>
public class BudgetOptions
{
    /// <summary>Master switch. Defaults true; set false to keep the section configured but inert
    /// (e.g. to toggle per-environment without removing config).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cumulative UTC-calendar-day USD ceiling. Once today's real recorded spend reaches this, the
    /// next call throws <see cref="GeminiBudgetExceededException"/> before being sent. Null = no
    /// daily ceiling (cost is still tracked/recorded, just not enforced).
    /// </summary>
    public decimal? MaxCostPerDayUsd { get; set; }

    /// <summary>
    /// Optional, best-effort single-request estimate ceiling. See the "why cumulative-first" design
    /// note in PLAN-cost-governance.md §4 for why this can only bound input cost precisely, and
    /// output cost only when the request sets <see cref="GeminiRequestOptions.MaxTokens"/>. Null
    /// (default) = not enforced.
    /// </summary>
    public decimal? MaxCostPerRequestUsd { get; set; }

    /// <summary>
    /// Per-model USD pricing. Falls back to <see cref="GeminiCostGovernor.DefaultPricing"/> (built-in,
    /// snapshot of Google's published rates at the time this library version shipped, so it will go
    /// stale; override here for accuracy) for any model not present in this dictionary. Keyed by the exact
    /// model string (e.g. <c>"gemini-3.6-flash"</c>).
    /// </summary>
    public IDictionary<string, ModelPricing>? ModelPricingOverrides { get; set; }
}

/// <summary>USD pricing per 1,000,000 tokens for one model.</summary>
public class ModelPricing
{
    public decimal InputPerMillionTokensUsd { get; set; }
    public decimal OutputPerMillionTokensUsd { get; set; }
    /// <summary>Price for tokens served from context caching. Typically well below the standard input rate.</summary>
    public decimal CachedInputPerMillionTokensUsd { get; set; }

    /// <summary>
    /// Optional second pricing tier for models that charge more above a token threshold (currently
    /// gemini-2.5-pro and gemini-3.1-pro-preview both do, at 200k tokens). Null = flat rate regardless
    /// of prompt size (true for every other current model).
    /// </summary>
    public HighVolumeTier? HighVolumeTier { get; set; }
}

/// <summary>The above-threshold pricing tier for models that have one.</summary>
public class HighVolumeTier
{
    /// <summary>Token count above which the higher rate applies (compared against the request's
    /// actual PromptTokenCount, since the tier is determined by realized usage, not an estimate).</summary>
    public int ThresholdTokens { get; set; }
    public decimal InputPerMillionTokensUsd { get; set; }
    public decimal OutputPerMillionTokensUsd { get; set; }
    public decimal CachedInputPerMillionTokensUsd { get; set; }
}
```

Cost formula: implement exactly this. Two details below are easy to get wrong. We found them by
reading the actual fields of `UsageMetadata`, and we did not assume them:

```
tier = (usage.PromptTokenCount > pricing.HighVolumeTier?.ThresholdTokens)
       ? pricing.HighVolumeTier : pricing (base rates)

billableInputTokens = usage.PromptTokenCount - usage.CachedContentTokenCount
inputCost  = billableInputTokens / 1_000_000m * tier.InputPerMillionTokensUsd
cachedCost = usage.CachedContentTokenCount / 1_000_000m * tier.CachedInputPerMillionTokensUsd

// ThoughtsTokenCount is billed as OUTPUT per Gemini's own docs (see the XML doc comment already on
// UsageMetadata.ThoughtsTokenCount: "billed as output"). A naive implementation that only prices
// CandidatesTokenCount would silently undercount cost on every thinking-enabled call.
outputCost = (usage.CandidatesTokenCount + usage.ThoughtsTokenCount) / 1_000_000m * tier.OutputPerMillionTokensUsd

totalCost = inputCost + cachedCost + outputCost
```

### 6.4 XML doc and README caveat to include verbatim

Both `BudgetOptions` and the new docs article (§9) must state the following prominently. Do not
bury it:

> This budget is tracked in-memory, per process. If your app runs as multiple instances (e.g.
> horizontally scaled), each instance tracks its own spend independently. The effective ceiling is
> `MaxCostPerDayUsd × instance count`, not a shared global cap. For a true cross-instance cap, you'd
> need to feed the `gemini.client.cost.usd` metric into an external system (e.g. a shared counter,
> your APM) and enforce there. This library does not attempt that.

## 7. Pricing table: verify before merging

We fetched this table from `https://ai.google.dev/gemini-api/docs/pricing` on 2026-08-02 (paid tier,
USD per 1M tokens). Re-fetch it and cross-check it against the live page before merging. Pricing
pages change. An automated fetch read this table, and nobody checked it by hand against a
screenshot.

| Model | Input | Output | Cached input | High-volume tier (>200k input tokens) |
|---|---|---|---|---|
| `gemini-3.6-flash` | $1.50 | $7.50 | $0.15 | N/A |
| `gemini-3.5-flash` | $1.50 | $9.00 | $0.15 | N/A |
| `gemini-3.5-flash-lite` | $0.30 | $2.50 | $0.03 | N/A |
| `gemini-3.1-pro-preview` | $2.00 | $12.00 | $0.20 | $4.00 / $18.00 / $0.40 |
| `gemini-3-flash-preview` | $0.50 | $3.00 | $0.05 | N/A |
| `gemini-3.1-flash-lite` | $0.25 | $1.50 | $0.025 | N/A |
| `gemini-2.5-pro` | $1.25 | $10.00 | $0.125 | $2.50 / $15.00 / $0.25 |
| `gemini-2.5-flash` | $0.30 | $2.50 | $0.03 | N/A |

Models that are not in this table have no built-in pricing entry. These are
`gemini-3.1-flash-image-preview`, `gemini-3-pro-image-preview`, older 1.x and pro models, and
embedding models. Decide on one consistent behavior for an unpriced model in the `RecordSpend` and
cost calculation, and document it. We recommend that you skip cost recording for that call. Record
token usage as normal, and do not compute or record a cost. Do not throw, and do not silently assume
$0. Either would corrupt the budget signal. Log a one-time warning (`_logger.LogWarning` on
`GeminiClient`, or equivalent) so the gap is visible without adding noise on every call. Add
image-generation model pricing as a fast follow, once that pricing is confirmed. Image-model pricing
is often per image and not per token. It needs separate design and not a token-rate entry. Do not
guess a token rate for image models.

## 8. Tests to add

Mirror the conventions of `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiRateLimiterTests.cs`
and `GeminiClientTests.cs` (xUnit, and `FakeHttpMessageHandler` for client-level tests). Add a new
file, `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiCostGovernorTests.cs`:

- The cost calculation is correct for a plain call (no caching, no thinking) against a known pricing
  entry.
- The cost calculation adds `ThoughtsTokenCount` to the output side and not the input side.
- The cost calculation discounts `CachedContentTokenCount` at the cached rate and not the standard
  input rate. It does not double-charge it. The standard-rate portion is `PromptTokenCount -
  CachedContentTokenCount` and not the full `PromptTokenCount`.
- High-volume tier: a response with `PromptTokenCount` above the threshold prices at the higher
  tier. At or below the threshold, it prices at the base tier.
- `CheckBudget()` throws `GeminiBudgetExceededException` once cumulative spend reaches
  `MaxCostPerDayUsd`. It does not throw one cent below it (an off-by-one boundary test).
- The `CreateDisabled()` equivalent: `CheckBudget()` never throws, whatever the spend. `RecordSpend`
  still returns and records a value (observability stays on when enforcement is off).
- Day rollover: spend recorded "yesterday" does not count toward today's ceiling. Inject or fake the
  clock. Do not rely on real wall-clock time in the test.
- Concurrency: N parallel `RecordSpend` calls all land, with no lost updates. This matches the
  thread-safety concern of the existing `GeminiResilienceTests.cs`, under this codebase's
  conventions.

Add to a new or extended file, `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiClientTests.cs`:
- `PostAsync` throws `GeminiBudgetExceededException` when the injected cost governor's
  `CheckBudget()` throws. The exception is unwrapped, and not wrapped in a generic
  `GeminiException`. The call count of the fake HTTP handler stays at 0, so the request never goes
  out.
- `PostAsync` calls `RecordSpend` with the response's real `Usage` after a successful call.
- `StreamAsync` calls `RecordSpend` exactly once, after the stream completes. It uses the
  `UsageMetadata` of the last chunk. Build a `FakeHttpMessageHandler` SSE response where only the
  final chunk carries `usageMetadata`, to match real Gemini behavior. Confirm that the existing
  `FakeHttpMessageHandler` helper in
  `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/FakeHttpMessageHandler.cs` supports a
  multi-chunk SSE body. If it does not, extend it minimally and do not duplicate a new helper.
- `StreamAsync` does not call `RecordSpend` if the stream is cancelled before the final chunk
  arrives.

Add a new file, `tests/Junaid.GoogleGemini.Net.Tests/Services/CountTokensModelOverloadTests.cs` (or
add to an existing relevant file). When `options.Model` and `Options.DefaultModel` are both set and
differ, the new `CountTokensAsync(prompt, options, ct)` overload resolves the endpoint with
`options.Model` and not `Options.DefaultModel`.

## 9. Docs to update

- Add `docs/articles/cost-governance.md`, following the structure and tone of the existing
  `docs/articles/resilience-and-rate-limiting.md`. Read that file first for the house style. Cover
  these points: what the feature does, and the difference between the cumulative budget and the
  per-request estimate from §4. Do not oversimplify this in the docs the way the in-conversation
  pitch did. Be precise. Cover the pricing override mechanism too. State the in-memory,
  single-instance caveat from §6.4 verbatim or near verbatim.
- Add it to `docs/articles/toc.yml` and to the guide list in `docs/index.md`, in the same way as
  every other article is linked from both places. Check the current entries of both files before you
  edit them. `toc.yml` was mid-edit in the working tree when we wrote this plan, because of the
  unrelated image-generation work. Do not clobber those changes.
- `README.md`: add one new row to the feature-comparison table near the top (match the existing row
  style: "Resilience built in", "Client-side rate limiting", and so on). Add a short code sample in
  the "Core features" section, in the same format as the existing `## Resilience & rate limiting`
  section near the bottom.
- `Junaid.GoogleGemini.Net.csproj`: add an entry to `PackageReleaseNotes` for this version bump.
  Check the current `<Version>` in the working csproj first. It is already at `6.1.0` as of this
  plan's writing, because of the uncommitted image-generation work. This feature should ship as part
  of that same next release, and not as a separate version. The exception is if that work has
  already been released by the time you implement this. Check `git log` and NuGet before you assume.

## 10. Correctness checklist (verify each item before you call this done)

- [ ] Every money computation uses `decimal`, never `float` or `double`. The one exception is the
      unavoidable `(double)` cast at the OTel `Counter<double>.Record` boundary, because OTel
      metrics are double-typed. Cast only at that final point, never earlier.
- [ ] `GeminiBudgetExceededException` extends `GeminiException` and not bare `Exception`.
      `PostAsync`'s existing `catch (GeminiException) { ...; throw; }` block then rethrows it
      unwrapped. This project made this exact mistake earlier, with `CassetteException`: a new
      exception type that did not extend the base was silently wrapped. We caught it. Do not repeat
      it.
- [ ] `ThoughtsTokenCount` goes on the output side of the cost formula. Do not ignore it and do not
      add it to the input side.
- [ ] The code prices `CachedContentTokenCount` at the cached rate and subtracts it from the
      standard-rate portion of `PromptTokenCount`. It does not charge it twice.
- [ ] High-volume tier selection uses the response's actual `PromptTokenCount`, and not an estimate.
- [ ] Streaming calls are cost-tracked (§5.3). This was the easiest piece to skip by accident, and
      skipping it breaks the guarantee of the whole feature.
- [ ] Cancelled or failed streams do not record a guessed partial cost.
- [ ] The disabled or no-op mode of `ICostGovernor` never throws. It matches the "always succeeds"
      precedent of `IRateLimiter.CreateDisabled()`. It does not follow a precedent of a silent no-op
      that does nothing.
- [ ] `GeminiOptions.Budget = null` (the default) adds zero overhead on the hot path. Confirm that
      `CheckBudget()` and `RecordSpend()` on the disabled implementation are cheap no-ops. They must
      not do wasted dictionary or lock work.
- [ ] New public types have XML doc comments. The core project has `TreatWarningsAsErrors=true` and
      suppresses only `CS1591`. Every existing public member in this codebase already has a proper
      doc comment regardless of that suppression. Match that standard. Do not rely on the
      suppression.
- [ ] The full solution build (`dotnet build Junaid.GoogleGemini.Net.sln -c Debug`) has 0 warnings
      and 0 errors. `dotnet test` is fully green on both net8.0 and net9.0. Check this before you
      consider the work done. This codebase has zero tolerance for build warnings on the core
      package.
- [ ] The `netstandard2.0` build target still compiles. `ConcurrentDictionary<DateOnly, decimal>` and
      `decimal` math are both fine on netstandard2.0. But double-check any new syntax you use (for
      example collection expressions or primary constructors). It must either appear already in the
      netstandard2.0-targeted parts of this codebase or be polyfilled. If you are unsure, check the
      `PolySharp` and `Microsoft.Bcl.AsyncInterfaces` setup in `Junaid.GoogleGemini.Net.csproj`.

## 11. Out of scope and explicit follow-ups (do not implement now, only note them)

- Cost tracking for embeddings, the Files API, and context-caching management.
- Image-generation model pricing (a different pricing model, likely per image and not per token).
- Cross-instance or distributed budget enforcement.
- Per-minute and per-hour budgets (only daily budgets exist).
- An official OTel GenAI semconv cost attribute, if and when one is standardized. Migrate the metric
  name then, and not now (see §6.2).
