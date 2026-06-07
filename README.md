# Junaid.GoogleGemini.Net

![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)
![NuGet](https://img.shields.io/nuget/v/Junaid.GoogleGemini.Net.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

A **production-ready** .NET client for the [Google Gemini API](https://ai.google.dev/) — resilient, observable, DI-native, and built to feel right in ASP.NET Core.

It covers the modern Gemini surface (structured output, system instructions, thinking, grounding, the Files API, context caching), but what makes it worth choosing is everything *around* the API call:

| | This library | Typical thin wrappers |
|---|---|---|
| **Resilience built in** | ✅ retries + backoff on the HttpClient pipeline | ❌ roll your own |
| **Client-side rate limiting** | ✅ token-bucket, configurable | ❌ none |
| **OpenTelemetry-native** | ✅ traces + token/latency metrics (GenAI semconv), zero extra deps | ❌ none |
| **`IChatClient` / `IEmbeddingGenerator`** | ✅ via companion package | sometimes |
| **Typed structured output** | ✅ `GenerateAsync<T>()` | rare |
| **DI-first + Options pattern** | ✅ | varies |

> **v6 is a modernization release with breaking changes** (idiomatic PascalCase models, `IAsyncEnumerable` streaming, typed exceptions). See [ROADMAP.md](ROADMAP.md).

## Installation

```shell
dotnet add package Junaid.GoogleGemini.Net
# optional: Microsoft.Extensions.AI adapters (IChatClient, IEmbeddingGenerator)
dotnet add package Junaid.GoogleGemini.Net.Extensions.AI
```

## Authentication

Get an API key from [Google AI Studio](https://aistudio.google.com/app/apikey), then provide it via environment variable (`GeminiApiKey`) or configuration:

```json
{
  "Gemini": {
    "ApiKey": "your-api-key-here",
    "DefaultModel": "gemini-2.5-flash",
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "RateLimit": { "Enabled": true, "RequestsPerMinute": 60 }
  }
}
```

## Quick start

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));

app.MapGet("/", async (IGeminiService gemini) =>
{
    var response = await gemini.GenerateAsync("Say hello!");
    return response.Text();
});
```

## Core features

### Generation, vision, chat

```csharp
// Text
var response = await gemini.GenerateAsync("Write a haiku about C#");

// Vision (text + image)
var image = new FileObject(File.ReadAllBytes("photo.jpg"), "photo.jpg");
var vision = await gemini.GenerateWithImageAsync("What's in this image?", image);

// Chat
var messages = new[]
{
    new MessageObject("user", "Hello, who are you?"),
    new MessageObject("model", "I'm Gemini."),
    new MessageObject("user", "Tell me a joke."),
};
var chat = await gemini.ChatAsync(messages);
```

### Streaming (`IAsyncEnumerable`)

```csharp
await foreach (var chunk in gemini.StreamAsync("Tell me a long story"))
{
    Console.Write(chunk.Text());
}

// Or a simple callback overload:
await gemini.StreamAsync("Tell me a long story", text => Console.Write(text));
```

### ⭐ Typed structured output

Get a strongly-typed result back — the JSON schema is derived from your type automatically:

```csharp
record Recipe(string Title, string[] Ingredients, int Minutes);

Recipe recipe = await gemini.GenerateAsync<Recipe>("A quick pasta recipe");
Console.WriteLine(recipe.Title);
```

### Reading responses safely

```csharp
var response = await gemini.GenerateAsync("...");

string text = response.Text();                 // "" if there was no text (never a placeholder)
if (response.TryGetText(out var t)) { /* ... */ }
string guaranteed = response.GetTextOrThrow();  // throws GeminiContentException if blocked/empty

var reason = response.FinishReason;             // e.g. "STOP", "SAFETY"
var usage  = response.Usage;                    // PromptTokenCount, CandidatesTokenCount, ...
```

### Request options (system instructions, thinking, JSON, grounding)

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini25Pro,
    Temperature = 0.7f,
    SystemInstruction = "You are a terse senior engineer.",
    ThinkingBudget = 1024,        // Gemini 2.5+ reasoning budget
    EnableGoogleSearch = true,    // ground answers with Google Search
};
var grounded = await gemini.GenerateAsync("What shipped in .NET 9?", options);
foreach (var q in grounded.Candidates?[0].GroundingMetadata?.WebSearchQueries ?? [])
    Console.WriteLine($"searched: {q}");
```

Convenience presets: `GeminiRequestOptions.Creative()`, `.Factual()`, `.Code()`, `.Fast()`.

### Embeddings

```csharp
var embedding = await embeddings.EmbedContentAsync(
    "gemini-embedding-001", "Your text",
    new EmbeddingOptions { TaskType = GeminiConstants.EmbeddingTaskTypes.RetrievalDocument });

var batch = await embeddings.BatchEmbedContentAsync("gemini-embedding-001", texts);
```

### Files API & context caching

```csharp
// Upload a file, wait until it's processed, then reference it
var file = await files.UploadFileAsync(bytes, "video/mp4", "clip.mp4");
await files.WaitUntilActiveAsync(file.Name!);

// Cache a large reusable context, then reference it by name to save tokens
var cache = await caching.CreateAsync(new CachedContent
{
    Model = "models/gemini-2.5-flash",
    Contents = [ /* large shared context */ ],
    Ttl = "3600s",
});
var answer = await gemini.GenerateAsync("Summarize.", new GeminiRequestOptions { CachedContent = cache.Name });
```

### Token counting & model info

```csharp
var tokens = await gemini.CountTokensAsync("Your text");
Console.WriteLine(tokens.TotalTokens);

var models = await modelInfo.ListModelsAsync();
```

## Microsoft.Extensions.AI integration

Use Gemini anywhere the .NET AI abstractions are consumed (Semantic Kernel, agent frameworks, middleware):

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));
builder.Services.AddGeminiChatClient("gemini-2.5-flash");          // registers IChatClient
builder.Services.AddGeminiEmbeddingGenerator("gemini-embedding-001"); // registers IEmbeddingGenerator

// elsewhere:
public class MyService(IChatClient chat)
{
    public Task<ChatResponse> Ask(string q) =>
        chat.GetResponseAsync([new ChatMessage(ChatRole.User, q)]);
}
```

## Observability (OpenTelemetry)

Traces and metrics are emitted via `System.Diagnostics` following the OTel GenAI conventions — no OpenTelemetry dependency is forced on you. Opt in:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(GeminiTelemetry.SourceName))
    .WithMetrics(m => m.AddMeter(GeminiTelemetry.SourceName));
```

You get per-call spans (`gen_ai.system`, `gen_ai.request.model`, token counts, finish reasons) and the `gen_ai.client.operation.duration` / `gen_ai.client.token.usage` metrics.

## Resilience & rate limiting

Configured once, applied automatically:

```csharp
builder.Services.AddGemini(options =>
{
    options.MaxRetries = 3;                       // retried on 429/5xx/transient with exponential backoff
    options.RetryBaseDelay = TimeSpan.FromSeconds(2);
    options.RateLimit.Enabled = true;             // client-side token bucket
    options.RateLimit.RequestsPerMinute = 60;
});
```

Failures surface as typed exceptions: `GeminiApiException` (status + parsed error), `GeminiRateLimitException`, `GeminiTimeoutException`, `GeminiSerializationException`, `GeminiContentException` — all deriving from `GeminiException`.

## Requirements

- **.NET 8.0 or .NET 9.0**
- A **Google AI Studio API key**

## Contributing & support

Issues and PRs welcome on [GitHub](https://github.com/jaslam94/Junaid.GoogleGemini.Net). Licensed under MIT.
