# Performance benchmarks

This library's pitch is "production-grade DX": retries, client-side rate limiting, cost
governance, and OpenTelemetry, all wired in by default. That naturally raises a question this
document exists to answer honestly. **What does all of that cost you, per call, versus a bare
`HttpClient`?**

## TL;DR

On a real network call to Gemini (typically 200ms-several seconds, more for "thinking" models),
everything measured here is noise. These numbers matter for two narrower audiences: very
high-throughput services calling Gemini thousands of times a second, and anyone who wants the
actual number instead of a marketing claim.

| Scenario | Mean latency | Allocated / call |
|---|---:|---:|
| Bare `HttpClient` (JSON serialize + POST + JSON parse, nothing else) | ~5 μs | 7.8 KB |
| `Junaid.GoogleGemini.Net`, default config (auth, retry/resilience, rate limiter, no budget, no OTel listener) | ~10 μs | 11.2 KB |
| `Junaid.GoogleGemini.Net`, everything on (+ daily budget enforcement, + a subscribed OTel `ActivityListener`/`MeterListener`) | ~11-12 μs | 12.2 KB |

So: **the full production-grade pipeline adds roughly 5-7 μs and 3.4-4.4 KB of garbage per call**
over the theoretical minimum. All three scenarios are measured against an in-memory fake HTTP
handler (see [Methodology](#methodology)). None of this includes real network time.

## Reading this honestly

A few things worth knowing before you use these numbers for anything:

- **This is one laptop, not a lab.** Wall-clock means moved by a few microseconds between runs on
  the same unchanged code, purely from OS/thermal/background-process noise (see the `Error`/`StdDev`
  columns in the raw output). The **allocated-bytes** numbers were rock solid run to run and are the
  more trustworthy figures here. Re-run `dotnet run -c Release --filter *` in
  [`benchmarks/Junaid.GoogleGemini.Net.Benchmarks`](../../benchmarks/Junaid.GoogleGemini.Net.Benchmarks)
  on your own hardware before making a capacity-planning decision off this page.
- **This measures non-streaming `generateContent` only.** Streaming, embeddings, and the Files/Batch
  APIs aren't covered yet.
- **"Default config" disables the rate limiter**, specifically to isolate CPU/allocation overhead
  from the limiter's *intentional* throttling delay. A benchmark loop fires thousands of calls a
  second; the default 60-requests/minute limiter would (correctly) start queuing almost
  immediately, and a benchmark that measured that would just be timing `Task.Delay`, not the
  library's code. With headroom under your configured RPM (the normal case, since you're not
  calling in a tight loop), the token bucket's fast path costs the same either way. See the doc
  comment on `GeminiClientDefaultBenchmarks` for the full reasoning.

## What one benchmark run found, and what changed as a result

Building this benchmark surfaced one real, if genuinely minor, inefficiency: `GeminiTelemetry`'s
metric recorders (`RecordUsage`, `RecordCost`, `RecordDuration`) unconditionally built a `TagList`
and called `Histogram<T>.Record`/`Counter<T>.Add` even when nothing was subscribed to listen. The
`Activity` (tracing) side of the same file already guarded this (`activity is { IsAllDataRequested:
true }`); the `Meter` (metrics) side didn't.

**Correction from an earlier draft of this page:** that first draft described this as avoiding
"boxing" the token/cost/duration values. A deeper review pass caught that this was wrong.
`TagList` only ever holds the library's own string tags here (`gen_ai.system`,
`gen_ai.request.model`, and so on); the numeric values are passed as `Record`/`Add`'s strongly-typed
generic parameter, which never boxes. There was no boxing to avoid. And since `Record`/`Add`
already no-op internally when `Enabled` is `false`, there was no allocation to avoid either.
`TagList`'s inline storage covers our handful of tags with no heap allocation regardless. What the
guard actually skips is smaller than that: the (cheap, non-allocating) work of building the
`TagList` and entering a call that was going to immediately no-op anyway.

Fixed by checking `Instrument<T>.Enabled` (the BCL's own "does anyone actually want this
measurement" flag) before doing that work, matching the pattern the tracing code already used. All
143 existing unit tests still pass, and a later pass added coverage that actually attaches a
`MeterListener` and asserts a real value gets recorded. The guarded branches had no test coverage
at all beforehand, so a future bug in this exact logic (say, swapping which token count gets tagged
`"input"` vs `"output"`) would previously have shipped silently. As expected given how small the
skipped work actually is, this benchmark's total-allocated-bytes numbers didn't move. The
telemetry tags were never the source of the ~3.4 KB gap between the raw and default scenarios,
which is dominated by legitimate feature costs (the correlation-ID GUID, the auth header, the
resilience pipeline, the rate limiter's lease object). It's shipped anyway because it's free and
correct, and matters more under sustained high-cardinality load, or when an app has OpenTelemetry
wired up for tracing but not metrics (or vice versa).

That is the actual, unglamorous shape of a "we benchmarked it and improved it" pass on a codebase
that's already reasonably tight: one small, honest fix, corrected once already above, which is the
point of writing this down rather than just asserting it, not a dramatic before/after headline.

## Methodology

Three [BenchmarkDotNet](https://benchmarkdotnet.org/) classes, each isolated in its own process
(BenchmarkDotNet's default), all hitting an in-memory `DelegatingHandler`
(`FakeGeminiHandler`) that returns a fixed, realistic `generateContent` response instantly. There is
no real network or disk I/O in any of them, so what's measured is exclusively this library's own
code:

- **`RawHttpClientBenchmarks`**: a bare `HttpClient`, one JSON serialize, one POST, one JSON parse.
  No auth, no retry, no rate limiter, no cost governance, no telemetry. The floor.
- **`GeminiClientDefaultBenchmarks`**: the real `AddGemini`-wired pipeline (auth handler,
  net8+'s `Microsoft.Extensions.Http.Resilience` retry handler, rate limiter, cost governor,
  telemetry), called the way an app actually would, via `IGeminiService.GenerateAsync(prompt)`.
  Default options: no budget configured, nobody subscribed to the `ActivitySource`/`Meter`.
- **`GeminiClientFullyObservedBenchmarks`**: identical to the above, but with a daily budget
  configured (so `ICostGovernor` runs its real check/record logic instead of the
  zero-overhead "not configured" short-circuit) and a live `ActivityListener` + `MeterListener`
  subscribed (so every span and metric actually gets recorded, not dropped as a no-op).

All three send the same prompt and the fake handler always returns the same response shape (one
candidate, 12 prompt tokens, 28 candidate tokens), so the comparison is apples to apples. Run with
`[MemoryDiagnoser]` on .NET 9, Release configuration.

To reproduce:

```shell
cd benchmarks/Junaid.GoogleGemini.Net.Benchmarks
dotnet run -c Release --filter "*"
```
