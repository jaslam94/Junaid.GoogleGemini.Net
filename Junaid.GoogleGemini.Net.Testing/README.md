# Junaid.GoogleGemini.Net.Testing

Record/replay HTTP cassettes for [Junaid.GoogleGemini.Net](https://www.nuget.org/packages/Junaid.GoogleGemini.Net) — write tests against the Gemini API that are deterministic, run offline, and don't need a live API key after the first recording.

## Why

Tests that hit a real LLM API are slow, cost money, and are flaky by nature (retries, rate limits, model non-determinism). Cassettes fix this the way [VCR](https://github.com/vcr/vcr) does for HTTP: record the real exchange once, replay it forever after.

## Install

```shell
dotnet add package Junaid.GoogleGemini.Net.Testing
```

## Usage

```csharp
using Junaid.GoogleGemini.Net.Testing;

services.AddGeminiWithCassette(
    options => options.ApiKey = Environment.GetEnvironmentVariable("GeminiApiKey") ?? "unused-in-replay",
    cassettePath: "Cassettes/generate-recipe.json");
```

Run the test once with a real API key set — it calls the live API and writes `Cassettes/generate-recipe.json`. Commit that file. Every later run (including CI, with no key at all) replays it: `CassetteMode.RecordOnce` (the default) only calls out for requests the cassette doesn't already have.

For full control, wire it into the HTTP pipeline directly:

```csharp
services.AddGemini(
    options => options.ApiKey = "unused-in-replay",
    pipeline => pipeline.AddCassette("Cassettes/generate-recipe.json", CassetteMode.Replay));
```

## Modes

| Mode | Real API call? | Use for |
|---|---|---|
| `RecordOnce` (default) | Only for requests not already recorded | Everyday local + CI use |
| `Replay` | Never — throws if a request isn't recorded | CI, to guarantee no accidental live call |
| `Record` | Always — overwrites the cassette | Refreshing a cassette after an API change |
| `Off` | Always — no cassette involved | Temporarily bypass |

## How matching works

Each request is matched against the cassette by HTTP method, endpoint, and JSON-semantic body equality (whitespace/formatting differences don't matter). Repeated identical requests replay in the order they were recorded.

## Safety

The cassette handler is registered as the *outermost* handler on the Gemini `HttpClient`, running before authentication attaches the API key. A cassette file can never contain your key, and replaying one never needs it.
