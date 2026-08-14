# Cost governance

Three mechanisms, all opt-in via `GeminiOptions.Budget`:

1. **Cost observability**: every response's real token usage is converted to a USD cost and
   recorded as an OpenTelemetry metric, following the same pattern as the existing token/duration
   metrics.
2. **Daily budget enforcement** (`MaxCostPerDayUsd`): a configurable daily USD ceiling. Once
   today's (UTC) cumulative *actual* spend reaches the ceiling, the **next** call is rejected before
   it's sent. This is the primary, exact mechanism.
3. **Per-request estimate enforcement** (`MaxCostPerRequestUsd`): a secondary, best-effort ceiling
   for a single call, checked before it's sent. It's necessarily an estimate, not exact. See below for
   exactly what it can and can't guarantee.

As far as we've been able to find, no other .NET Gemini client library offers this (checked nine,
including Google's own official SDK, as of August 2026; see the footnote on the README's
feature-comparison table for the full list).

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

Leave `options.Budget` unset (the default) to disable the feature entirely: zero overhead, nothing
tracked, nothing enforced.

## Why cumulative-first

The daily budget, not a per-request estimate, is the mechanism this feature is actually built
around. A design that only estimated per-request cost before sending ("call `countTokens`, price it,
compare to budget, then send") would be structurally incomplete on its own:

- `countTokens` only returns the *input* token count. It cannot know the *output* token count in
  advance, so a pre-flight check can only ever bound the input side precisely. The output side is
  unknown until generation completes (bounded only loosely by `MaxTokens`, if the caller set one).
- Spending an extra HTTP round-trip *before every single call, forever*, even when nothing is
  anywhere near budget, is a bad trade for a feature about controlling cost, if it's the *only*
  mechanism.

So the **primary, always-correct mechanism is cumulative**: after every real response, this library
computes its *actual* cost from the real `UsageMetadata`, adds it to a running daily total, and
records it via OpenTelemetry. Before the **next** call, it checks whether the running total has
already reached the ceiling, a cheap, in-memory, zero-extra-HTTP-call check. This is exact (built
from real billed tokens) and catches a runaway loop on the very next iteration, which is the actual
failure mode this feature exists to prevent.

`MaxCostPerRequestUsd` is a **secondary, opt-in** layer on top of that, for the different problem of
"reject this one outsized call before it happens" (for example, a user-supplied prompt that's
unexpectedly huge). Because it's opt-in, the extra round-trip cost is only paid by callers who choose
it. Leave `MaxCostPerRequestUsd` unset and no extra `CountTokensAsync` call is ever made;
`ICostGovernor.HasRequestCeiling` is checked first and is a cheap in-memory read, not a network call.

### What the per-request estimate can and can't guarantee

Read this before relying on `MaxCostPerRequestUsd`: it is a genuine estimate, not an exact figure.

- **Input cost is exact** for the prompt/messages/image tokens themselves (a real
  `CountTokensAsync(prompt, options, ct)` call happens first), but it does **not** include
  `SystemInstruction`, `Tools`, or `CachedContent` tokens, since the token-counting endpoint's request
  shape omits them, so it can undercount input for calls that use those.
- It always prices the full input at the **standard (non-cached) rate**, since it can't know ahead of
  time how much Gemini will actually serve from cache. This makes it conservative (an over-estimate,
  never an under-estimate) on that specific axis.
- **Output cost is only bounded when the request sets `GeminiRequestOptions.MaxTokens`** (widened by a
  positive `ThinkingBudget`, since thinking tokens bill as output too). Leave `MaxTokens` unset and
  only the input side is bounded; the real call's output cost is unknown until it completes.
- **Covers `ChatAsync(IList<Content>)`/`StreamChatAsync(IList<Content>)` too** (the raw multi-turn
  overloads used for function-calling/`thoughtSignature` replay), via
  `CountTokensChatAsync(IList<Content>, options, ct)`. Same general caveats as above; the token-counting
  request omits `SystemInstruction`/`Tools`/`CachedContent`, same as every other overload.
- **Enabling it doubles rate-limiter consumption per logical call**: the pre-flight
  `CountTokensAsync` call also goes through the client's rate limiter, so it consumes its own permit.
  Factor that into `RateLimitOptions.RequestsPerMinute` if you enable it.

If the estimate exceeds `MaxCostPerRequestUsd`, the call throws `GeminiRequestCostExceededException`
before the real (billed) request is ever sent.

## Streaming is covered too

`StreamAsync`/`StreamWithImageAsync`/`StreamChatAsync` are checked and tracked exactly like their
non-streaming counterparts (`GenerateAsync`/`GenerateWithImageAsync`/`ChatAsync`): both the daily
budget gate and, when configured, the per-request estimate. Otherwise a runaway streaming loop would
blow through the budget completely unchecked. Gemini's SSE stream carries `usageMetadata` on the
*final* chunk, so spend is recorded once the stream completes.

If a stream is cancelled or fails partway through, its cost is **not** recorded: this library has no
way to know the true billed amount without that final chunk, and recording a guessed partial figure
would be worse than recording nothing. A cancelled stream's actual server-side cost (whatever Google
billed for tokens already generated) won't be reflected in the tracked total.

## Pricing

Built-in pricing (`GeminiCostGovernor.DefaultPricing`) is a snapshot of Google's published per-model
rates at the time this library version shipped, and **will go stale**. Override it for accuracy:

```csharp
options.Budget.ModelPricingOverrides = new Dictionary<string, ModelPricing>
{
    ["gemini-3.6-flash"] = new ModelPricing
    {
        InputPerMillionTokensUsd = 1.50m,
        OutputPerMillionTokensUsd = 7.50m,
        CachedInputPerMillionTokensUsd = 0.15m,
    },
};
```

A model with no pricing entry (built-in or overridden) has its cost calculation skipped entirely:
token usage is still recorded as normal, but nothing is added to the running daily total and no
`gemini.client.cost.usd` metric is emitted for that call. The per-request estimate is likewise
skipped (never blocked) for an unpriced model. This library never silently assumes $0 for an unpriced
model, since that would corrupt the budget signal; it logs a one-time warning per model instead.

The cost formula accounts for two easy-to-miss details in `UsageMetadata`:

- **Thinking tokens are output, not input.** `ThoughtsTokenCount` is billed as output per Gemini's own
  docs, so it's added to `CandidatesTokenCount` before applying the output rate. Ignoring it would
  silently undercount every thinking-enabled call.
- **Cached tokens are priced once, at the cached rate.** `CachedContentTokenCount` is subtracted from
  `PromptTokenCount` before applying the standard input rate, then priced separately at the (usually
  much lower) cached rate, never both.

Two models (`gemini-2.5-pro`, `gemini-3.1-pro-preview`) have a higher rate above 200k input tokens.
The tier is selected from the response's *actual* `PromptTokenCount` (or, for the pre-flight
estimate, the exact counted input tokens), never an estimate of that count.

## Rejected calls

Two distinct exceptions, both `GeminiException` subtypes so they're catchable alongside the
library's other typed exceptions:

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

Both are thrown before any (further) HTTP work happens for the call being rejected. The rejected
call itself never reaches the network, so it costs nothing.

## Important limitation: single-process only

This budget is tracked **in-memory, per process**. If your app runs as multiple instances (for
example horizontally scaled), each instance tracks its own spend independently, so the effective
ceiling is `MaxCostPerDayUsd × instance count`, not a shared global cap. For a true cross-instance
cap, you'd need to feed the `gemini.client.cost.usd` metric into an external system (a shared
counter, or your APM) and enforce there. This library does not attempt that.

## Scope

Covers every call that returns `GenerateContentResponse` with `UsageMetadata`: text generation,
vision, chat, image generation, and their streaming counterparts. Not covered in this release:

- Embeddings (`EmbedContentResponse` carries no usage/token field today).
- The Files API and context-caching *management* calls (creating/deleting cached content). Only the
  *generation* calls that reference a `CachedContent` are covered, since they still return
  `GenerateContentResponse`.
- Per-minute/per-hour budgets. Daily (UTC calendar day) only, for now.
- Image-generation model pricing (`gemini-3.1-flash-image-preview`, `gemini-3-pro-image-preview`, and
  similar). Image pricing is often per-image, not per-token, so it needs separate design.
