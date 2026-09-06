# Junaid.GoogleGemini.Net

A **production-ready** .NET client for the [Google Gemini API](https://ai.google.dev/): resilient, observable, DI-native, and built to feel right in ASP.NET Core.

It covers the modern Gemini surface (structured output, system instructions, thinking, grounding, the Files API, context caching), but what makes it worth choosing is everything *around* the API call: built-in retries, client-side rate limiting, OpenTelemetry-native traces & metrics, and first-class `Microsoft.Extensions.AI` adapters.

## Get started

```shell
dotnet add package Junaid.GoogleGemini.Net
```

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));

app.MapGet("/", async (IGeminiService gemini) =>
    (await gemini.GenerateAsync("Say hello!")).Text());
```

## Guides

- [Getting started](articles/getting-started.md)
- [Structured output (`GenerateAsync<T>`)](articles/structured-output.md)
- [Streaming](articles/streaming.md)
- [Resilience & rate limiting](articles/resilience-and-rate-limiting.md)
- [Observability (OpenTelemetry)](articles/observability.md)
- [Microsoft.Extensions.AI integration](articles/extensions-ai.md)
- [Files & context caching](articles/files-and-caching.md)
- [Image generation](articles/image-generation.md)
- [Text-to-speech](articles/tts.md)
- [Cost governance](articles/cost-governance.md)
- [Batch API](articles/batch-api.md)
- [Migrating from v5 to v6](articles/migration-v5-to-v6.md)

## API reference

The full, generated [API reference](api/index.md) documents every public type and member.
