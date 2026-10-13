# Performance benchmarks

This library gives you retries, client-side rate limiting, cost governance, and OpenTelemetry,
all wired in by default. That raises a question this document answers. What does all of that cost
you per call, compared with a bare `HttpClient`?

## Summary

A real network call to Gemini typically takes 200ms to several seconds, and longer for "thinking"
models. Next to that, everything measured here is noise. The numbers matter to two groups: services
that call Gemini thousands of times a second, and anyone who wants the actual figure.

| Scenario | Mean latency | Allocated / call |
|---|---:|---:|
| Bare `HttpClient` (JSON serialize + POST + JSON parse, nothing else) | ~5 μs | 7.8 KB |
| `Junaid.GoogleGemini.Net`, default config (auth, retry/resilience, rate limiter, no budget, no OTel listener) | ~10 μs | 11.2 KB |
| `Junaid.GoogleGemini.Net`, everything on (+ daily budget enforcement, + a subscribed OTel `ActivityListener`/`MeterListener`) | ~11-12 μs | 12.2 KB |

The full pipeline adds roughly 5-7 μs and 3.4-4.4 KB of garbage per call over the theoretical
minimum. All three scenarios run against an in-memory fake HTTP handler (see
[Methodology](#methodology)). None of the numbers include real network time.

## How far to trust these numbers

Keep these points in mind:

- This is one laptop, not a lab. Wall-clock means moved by a few microseconds between runs of the
  same unchanged code. OS, thermal, and background-process noise caused that (see the `Error` and
  `StdDev` columns in the raw output). The allocated-bytes numbers stayed the same from run to run,
  so they are the more reliable figures. Before you make a capacity-planning decision from this
  page, run `dotnet run -c Release --filter *` in
  [`benchmarks/Junaid.GoogleGemini.Net.Benchmarks`](../../benchmarks/Junaid.GoogleGemini.Net.Benchmarks)
  on your own hardware.
- The benchmarks measure non-streaming `generateContent` only. They do not yet cover streaming,
  embeddings, or the Files and Batch APIs.
- The "default config" scenario disables the rate limiter. This isolates CPU and allocation overhead
  from the limiter's intentional throttling delay. A benchmark loop fires thousands of calls a
  second. The default limiter of 60 requests per minute would start queuing almost immediately, and
  the benchmark would then time `Task.Delay` and not the library's code. When your configured RPM
  leaves headroom (the normal case, because you do not call in a tight loop), the token bucket's
  fast path costs the same either way. See the doc comment on `GeminiClientDefaultBenchmarks` for
  the full reasoning.

## What one benchmark run found, and what changed

Building this benchmark exposed one real but minor inefficiency. The metric recorders in
`GeminiTelemetry` (`RecordUsage`, `RecordCost`, and `RecordDuration`) always built a `TagList` and
called `Histogram<T>.Record` or `Counter<T>.Add`, even when nothing was subscribed. The `Activity`
(tracing) side of the same file already guarded against this (`activity is { IsAllDataRequested:
true }`). The `Meter` (metrics) side did not.

An earlier draft of this page said the fix avoided "boxing" the token, cost, and duration values.
A deeper review showed that this was wrong. `TagList` holds only the library's own string tags here
(`gen_ai.system`, `gen_ai.request.model`, and so on). The code passes the numeric values as the
strongly typed generic parameter of `Record` and `Add`, which never boxes. So there was no boxing to
avoid. `Record` and `Add` already do nothing when `Enabled` is `false`, so there was no allocation to
avoid either. The inline storage of `TagList` holds our handful of tags without a heap allocation.
The guard skips less than that. It skips the cheap, non-allocating work of building the `TagList`
and entering a call that would do nothing anyway.

The fix checks `Instrument<T>.Enabled` before that work. This is the BCL's own flag for whether
anyone wants the measurement. It matches the pattern the tracing code already used. All 143 existing
unit tests still pass. A later pass added coverage that attaches a `MeterListener` and asserts that
the code records a real value. Before that, the guarded branches had no test coverage. A future bug
in this logic, such as swapping which token count gets the tag `"input"` or `"output"`, would have
shipped unnoticed.

The skipped work is small, so the total allocated bytes in this benchmark did not change. The
telemetry tags never caused the ~3.4 KB gap between the raw and default scenarios. Legitimate
feature costs cause most of that gap: the correlation-ID GUID, the auth header, the resilience
pipeline, and the rate limiter's lease object. We shipped the fix anyway because it is free and
correct. It matters more under sustained high-cardinality load, and when an app wires up
OpenTelemetry for tracing but not metrics (or the reverse).

This is what a benchmark pass on an already tight codebase looks like: one small fix, and one
correction to our own first explanation. We wrote it down so you can check the claim yourself.

## Methodology

The suite has three [BenchmarkDotNet](https://benchmarkdotnet.org/) classes. Each runs in its own
process (the BenchmarkDotNet default). All of them hit an in-memory `DelegatingHandler`
(`FakeGeminiHandler`) that instantly returns a fixed, realistic `generateContent` response. No
network or disk I/O is involved, so the benchmarks measure only this library's own code:

- `RawHttpClientBenchmarks`: a bare `HttpClient` that does one JSON serialize, one POST, and one
  JSON parse. It has no auth, retry, rate limiter, cost governance, or telemetry. This is the floor.
- `GeminiClientDefaultBenchmarks`: the real pipeline wired by `AddGemini`. It includes the auth
  handler, the retry handler from `Microsoft.Extensions.Http.Resilience` (net8 and later), the rate
  limiter, the cost governor, and telemetry. The benchmark calls it as an app would, through
  `IGeminiService.GenerateAsync(prompt)`. It uses default options: no budget configured, and nobody
  subscribed to the `ActivitySource` or `Meter`.
- `GeminiClientFullyObservedBenchmarks`: the same as the one above, with two changes. It configures
  a daily budget, so `ICostGovernor` runs its real check and record logic instead of the
  zero-overhead "not configured" short-circuit. It also subscribes a live `ActivityListener` and
  `MeterListener`, so every span and metric is recorded and not dropped as a no-op.

All three send the same prompt. The fake handler always returns the same response shape (one
candidate, 12 prompt tokens, 28 candidate tokens), so the comparison is fair. The runs use
`[MemoryDiagnoser]` on .NET 9 in the Release configuration.

To reproduce:

```shell
cd benchmarks/Junaid.GoogleGemini.Net.Benchmarks
dotnet run -c Release --filter "*"
```
