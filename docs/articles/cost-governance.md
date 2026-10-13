# Cost governance

Cost governance has three mechanisms. All of them are opt-in through `GeminiOptions.Budget`:

1. Cost observability. The library converts every response's real token usage to a USD cost. It
   records the cost as an OpenTelemetry metric, in the same way as the token and duration metrics.
2. Daily budget enforcement (`MaxCostPerDayUsd`). This is a daily USD ceiling. When today's (UTC)
   cumulative actual spend reaches the ceiling, the library rejects the next call before it sends
   it. This is the primary mechanism, and it is exact.
3. Per-request estimate enforcement (`MaxCostPerRequestUsd`). This is a secondary ceiling for one
   call, checked before the library sends it. It is an estimate, not an exact figure. See below for
   what it can and cannot guarantee.

We found no other .NET Gemini client library that offers this. We checked nine, including Google's
own official SDK, as of August 2026. See the footnote on the README's comparison table for the
full list.

```csharp
builder.Services.AddGemini(options =>
{
    options.Budget = new BudgetOptions
    {
        MaxCostPerDayUsd = 50.00m,     // reject the next call once today's real spend hits $50
        MaxCostPerRequestUsd = 2.00m,  // optional: reject one outsized call before it's sent
    };
});
```

Leave `options.Budget` unset (the default) to turn the feature off. Then it has no overhead, tracks
nothing, and enforces nothing.

## Why the daily budget comes first

The daily budget is the core of this feature. A design that only estimated per-request cost before
sending would be incomplete. That design calls `countTokens`, prices the result, compares it to the
budget, and then sends. It has two problems:

- `countTokens` returns only the input token count. It cannot know the output token count in
  advance. A pre-flight check can bound the input side exactly, but the output side stays unknown
  until generation completes. `MaxTokens` bounds it loosely, if the caller set one.
- An extra HTTP round-trip before every call is a poor trade for a feature that controls cost. This
  is true even when spend is nowhere near the budget, and it is worse if the check is the only
  mechanism.

So the primary mechanism is cumulative. After every real response, the library computes the actual
cost from the real `UsageMetadata`. It adds that cost to a running daily total and records it
through OpenTelemetry. Before the next call, it checks whether the running total has reached the
ceiling. This check is in-memory and makes no extra HTTP call. It is exact because it uses real
billed tokens. It stops a runaway loop on the next iteration, which is the failure this feature
exists to prevent.

`MaxCostPerRequestUsd` is a secondary, opt-in layer on top. It solves a different problem: reject
one outsized call before it happens, for example a user-supplied prompt that is unexpectedly huge.
Only callers who enable it pay for the extra round-trip. If you leave `MaxCostPerRequestUsd` unset,
the library never makes the extra `CountTokensAsync` call. It checks `ICostGovernor.HasRequestCeiling`
first, and that is a cheap in-memory read.

### What the per-request estimate can and cannot guarantee

Read this section before you rely on `MaxCostPerRequestUsd`. The figure is an estimate and is not
exact.

- Input cost is exact for the prompt, messages, and image tokens. A real
  `CountTokensAsync(prompt, options, ct)` call runs first. The count does not include
  `SystemInstruction`, `Tools`, or `CachedContent` tokens, because the token-counting endpoint's
  request shape omits them. For calls that use those, the estimate can undercount input.
- The estimate always prices the full input at the standard (non-cached) rate, because it cannot
  know in advance how much Gemini will serve from cache. On this point the estimate is
  conservative. It over-estimates and never under-estimates.
- Output cost has a bound only when the request sets `GeminiRequestOptions.MaxTokens`. A positive
  `ThinkingBudget` widens the bound, because thinking tokens bill as output too. If you leave
  `MaxTokens` unset, only the input side has a bound. The output cost stays unknown until the call
  completes.
- The estimate also covers `ChatAsync(IList<Content>)` and `StreamChatAsync(IList<Content>)`. These
  are the raw multi-turn overloads for function calling and `thoughtSignature` replay. They use
  `CountTokensChatAsync(IList<Content>, options, ct)`. The same caveats apply. The token-counting
  request omits `SystemInstruction`, `Tools`, and `CachedContent`, as it does for every other
  overload.
- Enabling it doubles rate-limiter use per logical call. The pre-flight `CountTokensAsync` call
  also goes through the client's rate limiter, so it uses its own permit. Allow for this in
  `RateLimitOptions.RequestsPerMinute` if you enable the estimate.

If the estimate exceeds `MaxCostPerRequestUsd`, the call throws `GeminiRequestCostExceededException`
before the library sends the real, billed request.

## Streaming is covered too

The library checks and tracks `StreamAsync`, `StreamWithImageAsync`, and `StreamChatAsync` in the
same way as their non-streaming counterparts (`GenerateAsync`, `GenerateWithImageAsync`, and
`ChatAsync`). That includes the daily budget gate and, when configured, the per-request estimate.
Without this, a runaway streaming loop could pass the budget unchecked. Gemini's SSE stream carries
`usageMetadata` on the final chunk, so the library records spend when the stream completes.

If a stream is cancelled or fails partway through, the library does not record its cost. Without
the final chunk, the library cannot know the true billed amount. A guessed partial figure would be
worse than no figure. Google still bills the tokens it already generated, but the tracked total
will not include them.

## Pricing

The built-in pricing (`GeminiCostGovernor.DefaultPricing`) is a snapshot of Google's published
per-model rates from the time this library version shipped. It will go stale. Override it to keep
it accurate:

```csharp
options.Budget.ModelPricingOverrides = new Dictionary<string, ModelPricing>
{
    ["gemini-3.6-flash"] = new ModelPricing
    {
        InputPerMillionTokensUsd = 0.75m,
        OutputPerMillionTokensUsd = 3.75m,
        CachedInputPerMillionTokensUsd = 0.075m,
    },
};
```

Watch for time-limited introductory pricing. Google charges a 50%-off introductory rate for
`gemini-3.6-flash`, `gemini-3.8-flash` and the two 3.8 TTS models through December 31, 2026. The
standard rate (double these numbers) starts on January 1, 2027.

The built-in table already knows both rates. Each entry carries its 2027 rate in
`ModelPricing.NextPricing`, and the governor switches to it on 2027-01-01 at 00:00 UTC. A long-running
process switches without a restart. Google's pricing page gives no time zone, so the exact hour is our
assumption.

You can schedule a change in your own overrides in the same way:

```csharp
["gemini-3.8-flash"] = new ModelPricing
{
    InputPerMillionTokensUsd = 0.75m,
    OutputPerMillionTokensUsd = 3.75m,
    CachedInputPerMillionTokensUsd = 0.075m,
    NextPricingEffectiveFromUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    NextPricing = new ModelPricing
    {
        InputPerMillionTokensUsd = 1.50m,
        OutputPerMillionTokensUsd = 7.50m,
        CachedInputPerMillionTokensUsd = 0.15m,
    },
},
```

Google deprecated `gemini-3.5-flash` and `gemini-3.7-flash` on October 8, 2026 and routes them to
`gemini-3.6-flash` and `gemini-3.8-flash`. The built-in table prices those two names at the rate of
the model that answers. Google's billing catalog still lists `gemini-3.5-flash` at $1.50 in and
$9.00 out per million tokens. But on 2026-10-12 Google's usage metering counted all 40 of our
`gemini-3.5-flash` calls under `gemini-3.6-flash`, and none under the old name. We take that as
strong evidence that the calls bill at the 3.6 rate. It is not proof, because we could not read an
invoice. If Google bills the old name at its catalog rate, the estimate is too low by half or more.

If a model has no pricing entry (built-in or overridden), the library skips the cost calculation.
It still records token usage as normal. It adds nothing to the running daily total and emits no
`gemini.client.cost.usd` metric for that call. It also skips the per-request estimate for an
unpriced model, and never blocks the call. The library never assumes $0 for an unpriced model,
because that would corrupt the budget signal. It logs a one-time warning per model instead.

The cost formula handles three details in `UsageMetadata` that are easy to miss:

- Thinking tokens are output, not input. Gemini's docs bill `ThoughtsTokenCount` as output. The
  library adds it to `CandidatesTokenCount` before it applies the output rate. If it ignored
  thinking tokens, it would undercount every thinking-enabled call.
- Cached tokens are priced once, at the cached rate. The library subtracts
  `CachedContentTokenCount` from `PromptTokenCount` before it applies the standard input rate. It
  then prices the cached tokens separately at the cached rate, which is usually much lower. It
  never prices a token at both rates.
- Tool-use tokens are input. With `Part.MediaProcessing = "AGENTIC"`, the model loads video frames,
  audio, and the transcript on demand. The API reports those tokens in `ToolUsePromptTokenCount`
  and not in `PromptTokenCount`. A real test showed a 660-token video as 11 prompt tokens. The
  library adds `ToolUsePromptTokenCount` to the input tokens at the standard input rate. It also
  counts them when it picks the high-volume tier. Google's billing catalog has no separate SKU for
  tool-use tokens, and every input SKU of `gemini-3.8-flash` costs the same. So the input rate is the
  only rate that can apply. We do not know whether Google bills these tokens at all, because its
  usage meter did not count them as input in our test. If it does not, the estimate is too high, which
  is the safe direction for a budget. Without this, the governor priced an agentic call at about a quarter of its real cost.

Two models (`gemini-2.5-pro` and `gemini-3.1-pro-preview`) have a higher rate above 200k input
tokens. The library picks the tier from the response's actual `PromptTokenCount`. For the
pre-flight estimate, it uses the exact counted input tokens. It never uses a guess of that count.

## Rejected calls

The library throws two exceptions for rejected calls. Both derive from `GeminiException`, so you can
catch them alongside the library's other typed exceptions:

```csharp
try
{
    var response = await gemini.GenerateAsync(prompt);
}
catch (GeminiBudgetExceededException ex)
{
    // The cumulative daily budget (MaxCostPerDayUsd) was already reached before this call.
    // ex.CurrentSpendUsd, ex.BudgetLimitUsd
}
catch (GeminiRequestCostExceededException ex)
{
    // This single call's estimated cost (MaxCostPerRequestUsd) exceeded the ceiling.
    // ex.EstimatedCostUsd, ex.MaxCostPerRequestUsd
}
```

The library throws both before it does any further HTTP work for the rejected call. A rejected call
never reaches the network, so it costs nothing.

## Limitation: single process only

The library tracks the budget in memory, per process. If your app runs as multiple instances (for
example, scaled horizontally), each instance tracks its own spend. The effective ceiling is then
`MaxCostPerDayUsd × instance count`, not a shared global cap. For a true cross-instance cap, feed
the `gemini.client.cost.usd` metric into an external system, such as a shared counter or your APM,
and enforce the cap there. This library does not do that.

## Scope

Cost governance covers every call that returns `GenerateContentResponse` with `UsageMetadata`. That
means text generation, vision, chat, image generation, and their streaming counterparts. This
release does not cover:

- Embeddings. `EmbedContentResponse` has no usage or token field today.
- The Files API and context-caching management calls (creating and deleting cached content). The
  library covers only the generation calls that reference a `CachedContent`, because they still
  return `GenerateContentResponse`.
- Per-minute and per-hour budgets. The library supports daily budgets only (UTC calendar day), for
  now.
- `gemini-2.5-flash-image`, the older "Nano Banana" model. Google prices its output per image and
  publishes no per-token equivalent. The four current image models (`gemini-nano-banana-2.1`,
  `gemini-3.1-flash-image`, `gemini-3-pro-image`, and `gemini-3.1-flash-lite-image`) are priced per
  1M tokens on input and output. They have built-in pricing entries like any text model.
- The [Batch API](batch-api.md) (`IBatchService`). It bypasses cost governance on purpose.
  `BatchService` uses its own `HttpClient` instead of the shared pipeline that `ICostGovernor` hooks
  into. Batch is also priced at a 50% discount that this library's pricing model does not yet
  represent.
