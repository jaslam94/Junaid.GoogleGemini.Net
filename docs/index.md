# Junaid.GoogleGemini.Net

A .NET client for the [Google Gemini API](https://ai.google.dev/). It has built-in resilience, OpenTelemetry support, and dependency injection, and it fits naturally into ASP.NET Core.

It covers the current Gemini API: structured output, system instructions, thinking, grounding, the Files API, and context caching. It also handles the work around the API call. That includes built-in retries, client-side rate limiting, OpenTelemetry traces and metrics, and `Microsoft.Extensions.AI` adapters.

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
- [Speech-to-text](articles/transcription.md)
- [Multimodal embeddings](articles/multimodal-embeddings.md)
- [Cost governance](articles/cost-governance.md)
- [Batch API](articles/batch-api.md)
- [Migrating from v5 to v6](articles/migration-v5-to-v6.md)

## API reference

The generated [API reference](api/index.md) documents every public type and member.
