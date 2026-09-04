# Plan: Cost governance for Junaid.GoogleGemini.Net

**Status:** Implemented and shipped in 6.2.0. Kept here as a historical record (code comments cross-
reference specific sections of this file, e.g. the cost formula in §6.3 and the design rationale in
§4). **Audience:** an AI coding agent (Cursor) implementing this directly against the current
`master` branch. **Author's confidence:** every file path, method signature, and integration point
below was verified by reading the actual current source on 2026-08-02, not from memory. Pricing
numbers were fetched from the live Google pricing page on the same date. Re-verify them before
merging (see §7).

**Post-implementation addendum:** §2, §4, and §11 describe `MaxCostPerRequestUsd` as in-scope but
leave its actual enforcement path unspecified. The `ICostGovernor` interface given in §5.1 has no
method for it, and no integration point, test, or checklist item covers wiring it up. It shipped in
two stages as a result: 6.2.0 first landed everything specified here exactly as written, with
`MaxCostPerRequestUsd` as a config-only property (no enforcement); a follow-up change in the same
6.2.0 release then added the actual pre-flight enforcement (`ICostGovernor.CheckEstimatedRequestCost`,
`ICostGovernor.HasRequestCeiling`, `GeminiRequestCostExceededException`, wired into `GeminiService`)
as a deliberate extension beyond this plan's literal text. See `docs/articles/cost-governance.md`
for what actually shipped, including limitations (e.g. no coverage for `ChatAsync(IList<Content>)`)
this plan doesn't anticipate.

---

## 1. Goal

Let a caller cap what a Gemini integration can spend, and observe what it actually spends, without
rolling their own token-counting and pricing math. Two mechanisms:

1. **Cost observability** (always on if the feature is enabled): every response's real token usage
   is converted to a USD cost and recorded as an OpenTelemetry metric, following the exact pattern
   already used for token/duration metrics.
2. **Budget enforcement** (opt-in): a configurable daily USD ceiling. Once cumulative *actual* spend
   for the day reaches the ceiling, the **next** call is rejected before it's sent. No network
   round-trip, no cost incurred by the rejected call itself.

## 2. Explicit scope (read this before writing any code)

**In scope:**
- `GenerateContentResponse`-based calls only, meaning everything that already flows through
  `GeminiClient.PostAsync`/`StreamAsync` and populates `GenerateContentResponse.UsageMetadata`. This
  is exactly the set of calls `GeminiTelemetry.RecordUsage` already instruments today.
- Both non-streaming (`PostAsync`) and streaming (`StreamAsync`) calls. **Streaming must be
  included**. A budget guardrail that silently doesn't apply to `StreamAsync`/`StreamChatAsync`
  would let a runaway streaming loop blow through the budget completely unchecked, which defeats the
  feature's entire purpose. See §5.3 for exactly what's missing there today.
- A **cumulative daily budget**, enforced from real recorded spend (exact, not estimated).
- An **optional, opt-in per-request estimate** (`MaxCostPerRequestUsd`), which is a best-effort
  guard only. See §4 for why it can't be exact.

**Explicitly out of scope for this change (do not implement):**
- Embeddings (`EmbedContentAsync`/`BatchEmbedContentAsync`). Verified: `EmbedContentResponse` has no
  usage/token field at all today, so there's nothing to cost from without separate design work.
- Files API and context caching operations themselves (the storage/management calls). Only the
  *generation* calls that reference a `CachedContent` are covered, and they're covered automatically
  since they still return `GenerateContentResponse`.
- Per-minute or per-hour budgets. Daily (UTC calendar day) only for v1.
- Multi-instance/distributed budget tracking (e.g. shared Redis-backed counter across horizontally
  scaled instances). This is in-process, single-instance, in-memory only. Document that limitation
  loudly (see §6.4).

## 3. Prerequisite bug fix (found during research, must land first)

`IGeminiService.CountTokensAsync(string prompt, CancellationToken ct)` has **no way to specify a
model**. Internally it always resolves to `Options.DefaultModel`
(`Junaid.GoogleGemini.Net/Services/GeminiService.cs`, `GetCountTokensEndpoint(null)` →
`FormatModelName(null, Options?.DefaultModel)`). This means: if a caller does

```csharp
await gemini.GenerateAsync(prompt, new GeminiRequestOptions { Model = "gemini-3.1-pro-preview" });
```

and this feature's per-request pre-flight estimate calls `CountTokensAsync(prompt)` to estimate
input cost, it will silently count tokens (and therefore price) against the *default* model, not
`gemini-3.1-pro-preview`, the model the call is actually going to use. Given the pricing table has
materially different rates per model (see §7), this would produce a wrong estimate for any call that
overrides `Model`.

**Fix:** add a new overload, do not change the existing one:

```csharp
// IGeminiService.cs: new overload, existing 2-arg one stays exactly as-is
Task<CountTokensResponse> CountTokensAsync(
    string prompt,
    GeminiRequestOptions? options,
    CancellationToken cancellationToken = default);
```

Implement in `GeminiService.cs` by threading `options?.Model` into `GetCountTokensEndpoint`
(currently hardcoded to `GetCountTokensEndpoint(null)` in the 2-arg overload. Change that call site
to delegate to the new 3-arg overload with `options: null`, and have the new overload pass
`options?.Model` through). Mirror the same fix for `CountTokensWithImageAsync` and
`CountTokensChatAsync` for consistency, even though this feature only needs the plain-text one.
leaving them inconsistent would be a foot-gun for the next person. Add the 3 new overloads to
`IGeminiService.cs` with XML docs; keep the 3 existing overloads calling through to the new ones with
`options: null`.

This is a real, independently-worthwhile bug fix. Call it out as such in the PR description, separate
from the cost-governance feature itself.

## 4. Why cumulative-first, not "count tokens before every call"

The naive design ("before every call, call `countTokens`, estimate cost, compare to budget, then
send the real call") was rejected. It doubles the number of API calls (an extra HTTP round-trip
*before every single generation call*, forever, even when nothing is near budget), which is a bad
trade for a feature literally named "cost governance." It's also structurally incomplete: `countTokens`
only returns `TotalTokens` for the *input*. It cannot know the *output* token count in advance, so
even a "pre-flight" check can only ever bound the input side precisely; the output side is
fundamentally unknown until generation completes (bounded only loosely by `MaxTokens`, if the caller
set one).

**Design actually used:**
- The **primary, always-correct mechanism** is cumulative: after every real response, compute its
  *actual* cost from the real `UsageMetadata`, add it to a running daily total, and record it via
  OTel. Before sending the **next** call, check whether the running total has already reached the
  configured ceiling, a cheap, in-memory, zero-extra-HTTP-call check. This is exact (built from real
  billed tokens) and catches the "$4k overnight" runaway-loop scenario on the very next iteration,
  which is the actual failure mode this feature exists to prevent.
- The **secondary, opt-in mechanism** (`MaxCostPerRequestUsd`) is an estimate: input cost is exact
  (via the fixed `CountTokensAsync` overload from §3), output cost is bounded by `MaxTokens` if the
  caller set one, else a configurable worst-case assumption (default: treat it as unbounded / skip
  the output half of the estimate and document that it only bounds input cost unless `MaxTokens` is
  set). Document this precisely. Do not let the XML doc comment imply a guarantee it can't make.

## 5. Integration points (exact files, exact locations)

### 5.1 New interface + implementation, mirroring `IRateLimiter`/`GeminiRateLimiter` exactly

Read `Junaid.GoogleGemini.Net/Infrastructure/GeminiRateLimiter.cs` first. This new component must
follow the same shape: an interface + a real implementation + a `CreateDisabled()` factory for the
zero-overhead-when-unused case, injected into `GeminiClient`'s constructor exactly like
`IRateLimiter` is.

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

Constructor: add `ICostGovernor costGovernor` as a fourth constructor parameter, stored in a new
`_costGovernor` field, same null-check pattern as the other three (`ArgumentNullException` if null).

In `PostAsync<TRequest, TResponse>`, immediately after the existing rate-limiter gate
(`using var lease = await _rateLimiter.AcquireAsync(...)`, around line 94-98) and before the request
is built, add:

```csharp
_costGovernor.CheckBudget();
```

This throws `GeminiBudgetExceededException` (a `GeminiException` subtype, see §6.1) before any HTTP
work happens, exactly mirroring how the rate-limiter's `GeminiRateLimitException` is thrown before
the request is built. No `try/catch` needed here. `GeminiBudgetExceededException` will be caught by
the existing `catch (GeminiException) { activity?.SetStatus(...); throw; }` block already in
`PostAsync` (verified present at line ~120), so it propagates cleanly without being wrapped by the
generic `catch (Exception ex)` handler.

Then, in the existing block that already does telemetry recording:

```csharp
if (result is GenerateContentResponse contentResponse)
{
    GeminiTelemetry.RecordUsage(operation, model, contentResponse.Usage, contentResponse.FinishReason, activity);
}
```

add, immediately after:

```csharp
if (result is GenerateContentResponse { Usage: not null } usageResponse)
{
    _costGovernor.RecordSpend(model, usageResponse.Usage!);
}
```

(Two separate `is` checks rather than reusing `contentResponse` from above is deliberate. Keep this
patch minimal and additive rather than restructuring the existing conditional, to keep the diff easy
to review.)

### 5.3 `GeminiClient.StreamAsync`: the currently-missing piece

File: same, `StreamAsync<TRequest>`. **Important finding:** `StreamAsync` today has **zero**
telemetry instrumentation of any kind: no `GeminiTelemetry.StartOperation`, no `RecordUsage`, no
`RecordDuration`. Only `PostAsync` is instrumented. This is a pre-existing, already-acknowledged gap
(unrelated to this feature), but cost governance **cannot silently inherit it**. If streaming calls
aren't cost-tracked, the daily budget ceiling is trivially bypassable by anyone using
`StreamAsync`/`StreamChatAsync`, which are documented, first-class APIs.

**Required additions to `StreamAsync`:**
1. Add the same `_costGovernor.CheckBudget()` call at the top, in the same place the rate-limiter
   lease is acquired (right after `using var lease = await _rateLimiter.AcquireAsync(...)`).
2. Gemini's SSE stream carries `usageMetadata` on the **final** chunk (intermediate chunks typically
   omit it or carry partial data). Track the most recent non-null `UsageMetadata` seen across yielded
   chunks: add a local `UsageMetadata? finalUsage = null;` before the `while (true)` loop, and after
   each successful `yield return chunk;` (both the per-blank-line one and the trailing-flush one at
   the end of the method), do `finalUsage = chunk.UsageMetadata ?? finalUsage;`.
3. After the stream completes successfully (i.e. after the `while` loop and the trailing-flush block,
   inside the existing `try`, before the `finally`), if `finalUsage is not null`, call
   `_costGovernor.RecordSpend(model, finalUsage)`. You'll need `model` in scope. `StreamAsync`
   currently doesn't call `GeminiTelemetry.Parse(endpoint)` at all; add that at the top of the method
   (same one-liner `PostAsync` already uses) purely to get `model` for this purpose. Do **not** add
   full duration/operation telemetry to `StreamAsync` as part of this change. That's the
   pre-existing, separately-tracked gap; stay scoped to what cost governance needs (just the model
   name and the final usage snapshot).
4. If the stream is cancelled or throws partway through, do **not** record spend for whatever partial
   usage was seen. Gemini bills for what the server actually generated regardless of whether the
   client kept reading, but this library has no way to know the true billed amount without the
   final `usageMetadata`, so recording a partial/guessed figure would be worse than recording
   nothing. Document this as a known limitation (a cancelled stream's actual cost won't be reflected
   in the tracked total) rather than silently guessing.

### 5.4 DI registration

File: `Junaid.GoogleGemini.Net/Extensions/GeminiExtensions.cs`, inside `AddGemini(services,
configureOptions)`.

Add, near the existing rate-limiter registration (`services.AddSingleton<IRateLimiter>(...)`):

```csharp
services.AddSingleton<ICostGovernor>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
    return new GeminiCostGovernor(options.Budget, GeminiTelemetry.RecordCost);
});
```

(Exact constructor shape is an implementation detail for Cursor to finalize. The point is: singleton
lifetime, same as the rate limiter, built from `GeminiOptions.Budget`.)

`GeminiClient` is registered via `services.AddHttpClient<GeminiClient>(...)`. Since `GeminiClient`'s
constructor now takes `ICostGovernor` as a fourth parameter, standard constructor injection picks it
up automatically; no explicit wiring needed beyond the `AddSingleton<ICostGovernor>` line above.

## 6. New types

### 6.1 Exception

New file `Junaid.GoogleGemini.Net/Exceptions/GeminiBudgetExceededException.cs`, following the exact
pattern of the existing `GeminiRateLimitException` in `GeminiExceptions.cs` (extra properties beyond
the message, same constructor style):

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

Put it in `GeminiExceptions.cs` alongside the others (matches existing file organization: all
non-`GeminiApiException` typed exceptions live in that one file) rather than a new file, unless
Cursor's judgment is that the file is getting too large (currently ~68 lines; adding one more class
keeps it well under any reasonable limit, so prefer the existing file).

### 6.2 Telemetry

File: `Junaid.GoogleGemini.Net/Infrastructure/Telemetry/GeminiTelemetry.cs`. Add alongside the
existing `TokenUsage`/`OperationDuration` histograms:

```csharp
internal static readonly Counter<double> CostUsd = Meter.CreateCounter<double>(
    "gemini.client.cost.usd", unit: "USD", description: "Cumulative USD cost of Gemini client operations.");
```

**Naming note, do not get this wrong:** the existing metrics (`gen_ai.client.operation.duration`,
`gen_ai.client.token.usage`) use the `gen_ai.*` prefix because those are *stable, official OpenTelemetry
GenAI semantic convention* attribute names. As of this plan's writing, **there is no official
`gen_ai.client.cost.*` metric in the OTel GenAI semconv spec**. It would be dishonest to imply
standards compliance that doesn't exist by reusing that prefix. Use `gemini.client.cost.usd` (this
library's own namespace, matching `GeminiTelemetry.SystemName = "gemini"`) instead. If a future OTel
semconv version adds an official cost attribute, that's a deliberate follow-up migration, not
something to guess at now. Before implementing, do a quick check of the current OTel GenAI semconv
spec in case this has changed since. If an official name now exists, use that instead and note the
change in the PR description.

Use a `Counter<double>`, not a `Histogram<double>`. Cost is cumulative/additive by nature (this
matches how billing dashboards represent spend), whereas the existing histograms model a
distribution of individual values (duration, token count per call). Tag it the same way
`RecordUsage`/`RecordDuration` already tag their metrics (`gen_ai.system`, `gen_ai.operation.name`,
`gen_ai.request.model`), for consistency and so it correlates in any dashboard alongside the existing
metrics.

Add a method mirroring `RecordUsage`'s shape:

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
new nested options class, following the exact style of the existing `RateLimitOptions`:

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

**Cost formula** (implement exactly this. The two nuances below are easy to get wrong and were
found by reading `UsageMetadata`'s actual fields, not assumed):

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

### 6.4 XML doc / README caveat to include verbatim

Both `BudgetOptions` and the new docs article (§9) must state, prominently, not buried:

> This budget is tracked **in-memory, per process**. If your app runs as multiple instances (e.g.
> horizontally scaled), each instance tracks its own spend independently. The effective ceiling is
> `MaxCostPerDayUsd × instance count`, not a shared global cap. For a true cross-instance cap, you'd
> need to feed the `gemini.client.cost.usd` metric into an external system (e.g. a shared counter,
> your APM) and enforce there. This library does not attempt that.

## 7. Pricing table: verify before merging

Fetched from `https://ai.google.dev/gemini-api/docs/pricing` on 2026-08-02 (paid tier, USD per 1M
tokens). **Re-fetch and cross-check this table against the live page before merging.** Pricing pages
change, and this was read by an automated fetch, not hand-verified against a screenshot.

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

Models not in this table (`gemini-3.1-flash-image-preview`, `gemini-3-pro-image-preview`, older
1.x/pro models, embedding models) have **no built-in pricing entry**. Decide and document one
consistent behavior for an unpriced model in `RecordSpend`/cost calc: recommended is to skip cost
recording for that call (record token usage as normal, just don't compute/record a cost. Do **not**
throw, and do **not** silently assume $0, since either would corrupt the budget signal) and log a
one-time warning (`_logger.LogWarning` on `GeminiClient`, or equivalent) so the gap is visible without
being noisy on every call. Add image-generation model pricing as a fast-follow once that pricing is
confirmed (image-model pricing is often per-image, not per-token, so it needs separate design, not a
token-rate entry, do not guess a token rate for image models).

## 8. Tests to add

Mirror `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiRateLimiterTests.cs` and
`GeminiClientTests.cs` conventions (xUnit, `FakeHttpMessageHandler` for client-level tests). New file
`tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiCostGovernorTests.cs`:

- Cost calc is correct for a plain call (no caching, no thinking) against a known pricing entry.
- Cost calc correctly adds `ThoughtsTokenCount` into the output side, not the input side.
- Cost calc correctly discounts `CachedContentTokenCount` at the cached rate, not the standard input
  rate, and does not double-charge it (i.e. `PromptTokenCount - CachedContentTokenCount` for the
  standard-rate portion, not the full `PromptTokenCount`).
- High-volume tier: a response with `PromptTokenCount` above the threshold prices at the higher tier;
  at or below, the base tier.
- `CheckBudget()` throws `GeminiBudgetExceededException` once cumulative spend reaches
  `MaxCostPerDayUsd`, and does not throw one cent below it (off-by-one boundary test).
- `CreateDisabled()`-equivalent: `CheckBudget()` never throws regardless of spend; `RecordSpend`
  still returns/records a value (observability stays on when enforcement is off).
- Day rollover: spend recorded "yesterday" (inject/fake the clock, don't rely on real wall-clock time
  in the test) does not count toward today's ceiling.
- Concurrency: N parallel `RecordSpend` calls all land (no lost updates). Matches the existing
  `GeminiResilienceTests.cs`-style concern for thread-safety under this codebase's conventions.

New/extended file `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/GeminiClientTests.cs`:
- `PostAsync` throws `GeminiBudgetExceededException` (unwrapped, not wrapped in a generic
  `GeminiException`) when the injected cost governor's `CheckBudget()` throws, and the fake HTTP
  handler's call count stays at 0 (the request never actually goes out).
- `PostAsync` calls `RecordSpend` with the response's real `Usage` after a successful call.
- `StreamAsync` calls `RecordSpend` exactly once, after the stream completes, using the last chunk's
  `UsageMetadata` (build a `FakeHttpMessageHandler` SSE response where only the final chunk carries
  `usageMetadata`, matching real Gemini behavior. Confirm the existing `FakeHttpMessageHandler`
  helper in `tests/Junaid.GoogleGemini.Net.Tests/Infrastructure/FakeHttpMessageHandler.cs` supports
  building a multi-chunk SSE body; if not, extend it minimally rather than duplicating a new helper).
- `StreamAsync` does NOT call `RecordSpend` if the stream is cancelled before the final chunk arrives.

New file `tests/Junaid.GoogleGemini.Net.Tests/Services/CountTokensModelOverloadTests.cs` (or add to
an existing relevant file): the new `CountTokensAsync(prompt, options, ct)` overload resolves the
endpoint using `options.Model`, not `Options.DefaultModel`, when both are set and differ.

## 9. Docs to update

- New `docs/articles/cost-governance.md`, following the structure/tone of the existing
  `docs/articles/resilience-and-rate-limiting.md` (read that file first for the house style before
  writing this one). Cover: what it does, the cumulative-vs-per-request distinction from §4 (don't
  oversimplify this in the docs the way the in-conversation pitch did, be precise), the pricing
  override mechanism, and the in-memory/single-instance caveat from §6.4 verbatim or near-verbatim.
- Add it to `docs/articles/toc.yml` and `docs/index.md`'s guide list, matching how every other
  article is already linked from both places (verify both files' current entries before editing:
  `toc.yml` was mid-edit in the working tree as of this plan's writing, from the unrelated image-
  generation work; don't clobber those changes).
- `README.md`: one new bullet in the feature-comparison table near the top (matching the existing
  row style: "Resilience built in", "Client-side rate limiting", etc.) plus a short code sample in
  the "Core features" section, same format as the existing `## Resilience & rate limiting` section
  near the bottom.
- `Junaid.GoogleGemini.Net.csproj`'s `PackageReleaseNotes`: add an entry for this version bump
  (check the current `<Version>` in the working csproj first; it's already at `6.1.0` as of this
  plan's writing, from the uncommitted image-generation work; this feature should ship as part of
  that same next release rather than inventing a separate version, unless that work has already been
  released by the time this is implemented. Check `git log`/NuGet before assuming).

## 10. Correctness checklist (verify each before calling this done)

- [ ] `decimal` used for every money computation, never `float`/`double` (except the unavoidable
      `(double)` cast at the OTel `Counter<double>.Record` boundary, since OTel metrics are
      double-typed. Cast only at that final point, never earlier).
- [ ] `GeminiBudgetExceededException` extends `GeminiException` (not bare `Exception`) so it's
      rethrown unwrapped by `PostAsync`'s existing `catch (GeminiException) { ...; throw; }` block.
      this exact mistake (a new exception type not extending the base, getting silently wrapped) was
      made and caught earlier in this project's history for `CassetteException`; don't repeat it.
- [ ] `ThoughtsTokenCount` is added to the *output* side of the cost formula, not ignored and not
      added to input.
- [ ] `CachedContentTokenCount` is priced at the cached rate and *subtracted* from the standard-rate
      portion of `PromptTokenCount` (not charged twice).
- [ ] High-volume tier selection uses the response's *actual* `PromptTokenCount`, not an estimate.
- [ ] Streaming calls are cost-tracked (§5.3). This was the single easiest piece to accidentally
      skip, and skipping it breaks the whole feature's guarantee.
- [ ] Cancelled/failed streams do not record a guessed partial cost.
- [ ] `ICostGovernor`'s disabled/no-op mode never throws, matching `IRateLimiter.CreateDisabled()`'s
      "always succeeds" precedent, not a silent-no-op-that-does-nothing precedent.
- [ ] `GeminiOptions.Budget = null` (the default) results in zero added overhead on the hot path.
      confirm `CheckBudget()`/`RecordSpend()` on the disabled implementation are cheap no-ops, not
      doing wasted dictionary/lock work.
- [ ] New public types have XML doc comments (the core project has `TreatWarningsAsErrors=true` and
      only suppresses `CS1591`, but every existing public member in this codebase already has a
      proper doc comment regardless of that suppression; match that standard, don't rely on the
      suppression).
- [ ] Full solution build (`dotnet build Junaid.GoogleGemini.Net.sln -c Debug`) is 0 warnings, 0
      errors, and `dotnet test` is fully green on both net8.0 and net9.0, before considering this
      done. This codebase has zero tolerance for build warnings on the core package.
- [ ] `netstandard2.0` build target still compiles. `ConcurrentDictionary<DateOnly, decimal>` and
      `decimal` math are both fine on netstandard2.0, but double-check any new syntax used (e.g.
      collection expressions, primary constructors) is either already used elsewhere in the
      netstandard2.0-targeted parts of this codebase or is polyfilled. Check
      `Junaid.GoogleGemini.Net.csproj`'s `PolySharp`/`Microsoft.Bcl.AsyncInterfaces` setup if unsure.

## 11. Out of scope / explicit follow-ups (do not implement now, just note them)

- Embeddings, Files API, context-caching-management cost tracking.
- Image-generation model pricing (different pricing model, likely per-image not per-token).
- Cross-instance/distributed budget enforcement.
- Per-minute/per-hour budgets (only daily).
- An official OTel GenAI semconv cost attribute, if/when one is standardized. Migrate the metric
  name then, not now (see §6.2).
