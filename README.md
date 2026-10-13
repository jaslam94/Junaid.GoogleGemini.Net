# Junaid.GoogleGemini.Net

![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)
![NuGet](https://img.shields.io/nuget/v/Junaid.GoogleGemini.Net.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

A .NET client for the [Google Gemini API](https://ai.google.dev/). It has built-in resilience, OpenTelemetry support, and dependency injection, and it fits naturally into ASP.NET Core.

> Claude (Anthropic) wrote the v5 to v6 modernization from start to finish. See [AI-assisted development](#ai-assisted-development).

It covers the current Gemini API: structured output, system instructions, thinking, grounding, the Files API, and context caching. It also handles the work around the API call:

| | This library | Typical thin wrappers |
|---|---|---|
| Resilience built in | Yes, retries and backoff on the HttpClient pipeline | No, roll your own |
| Client-side rate limiting | Yes, token-bucket, configurable | No |
| OpenTelemetry-native | Yes, traces and token/latency metrics (GenAI semconv), zero extra deps | No |
| `IChatClient` / `IEmbeddingGenerator` | Yes, via companion package | Sometimes |
| Typed structured output | Yes, `GenerateAsync<T>()` | Rare |
| DI-first + Options pattern | Yes | Varies |
| Cost governance | Yes, daily USD budget, per-request estimate, and cost metric | Not found in any .NET Gemini client we surveyed[^cost-governance-survey] |

[^cost-governance-survey]: Checked as of August 2026: Google's own [Google.GenAI](https://github.com/googleapis/dotnet-genai), [Mscc.GenerativeAI](https://github.com/mscraftsman/generative-ai), [Google_GenerativeAI](https://github.com/gunpal5/Google_GenerativeAI) (784K+ downloads, self-described "most complete" .NET Gemini SDK), [GeminiDotnet](https://github.com/rabuckley/GeminiDotnet), [Gemini.NET](https://github.com/phanxuanquang/Gemini.NET), [dotnet-gemini-sdk](https://github.com/gsilvamartin/dotnet-gemini-sdk), [Google_Generative_AI](https://github.com/tryAGI/Google_Generative_AI), and [GemiNet](https://github.com/nuskey8/GemiNet). None of them offer budget caps, cost tracking, or spend-limit enforcement. The closest any come is passing through the server's own 429 rate-limit errors as a typed exception, which is not the same thing.

> v6 has breaking changes: PascalCase models, `IAsyncEnumerable` streaming, and typed exceptions. See [ROADMAP.md](ROADMAP.md).

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
    "DefaultModel": "gemini-3.8-flash",
    "TimeoutSeconds": 100,
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

### Typed structured output

Get a strongly-typed result back. The JSON schema is derived from your type automatically:

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
var model  = response.ModelVersion;             // the model that answered, e.g. after Google reroutes a name
```

### Request options (system instructions, thinking, JSON, grounding)

```csharp
var options = new GeminiRequestOptions
{
    Model = GeminiConstants.Models.Gemini38Flash,        // Gemini 3 (the default is gemini-3.8-flash)
    SystemInstruction = "You are a terse senior engineer.",
    ThinkingLevel = GeminiConstants.ThinkingLevels.Low,  // Gemini 3 reasoning depth (use ThinkingBudget on 2.5)
    EnableGoogleSearch = true,                           // ground answers with Google Search
};
var grounded = await gemini.GenerateAsync("What shipped in .NET 9?", options);
foreach (var q in grounded.Candidates?[0].GroundingMetadata?.WebSearchQueries ?? [])
    Console.WriteLine($"searched: {q}");
```

Convenience presets: `GeminiRequestOptions.Creative()`, `.Factual()`, `.Code()`, `.Fast()`.

> **Note:** Google deprecated `temperature`/`topP`/`topK` on `gemini-3.8-flash`, `gemini-3.6-flash`,
> and `gemini-3.5-flash-lite` (July to September 2026). Those models ignore the sampling parameters,
> so `.Factual()` and `.Code()` have no effect on the default model. Google plans to return HTTP 400
> for these parameters on future model generations. Use `SystemInstruction` with explicit rules
> instead. These models also reject `PresencePenalty` and `FrequencyPenalty` with HTTP 400. See the
> XML docs on `GenerationConfig` before you set either.

> **Note:** Google deprecated `gemini-3.5-flash` and `gemini-3.7-flash` on October 8, 2026. It routes
> them to `gemini-3.6-flash` and `gemini-3.8-flash`. Change your model string to the new name.
> `gemini-3.8-flash` also rejects `ThinkingLevel = "minimal"` with HTTP 400. Use `low`, `medium` or
> `high`.

> **Gemini 3.** The library does not allow-list model names, so new models work without a library
> update. It supports `ThinkingLevel` and `MediaResolution`. It also captures the model's encrypted
> `thoughtSignature`. You can replay it for multi-turn function calling through the `Content`-based
> `ChatAsync` and `StreamChatAsync` overloads. Gemini 3 thinking models reason deeply by default, so
> set a lower `ThinkingLevel` for latency-sensitive calls.

### Image generation

```csharp
var response = await gemini.GenerateImageAsync("A watercolor painting of a lighthouse at sunset.");

foreach (var image in response.Images())
    await File.WriteAllBytesAsync($"lighthouse.{image.MimeType.Split('/')[1]}", image.Data);
```

Defaults to the efficient flash image model; pass `Model = GeminiConstants.Models.Gemini3ProImage` for
higher quality, or `ImageAspectRatio`/`ImageSize` (Gemini 3+ image models) for finer control. Google's
newer Nano Banana 2.1 is available as `GeminiConstants.Models.GeminiNanoBanana21`. It is not the
default yet, because it sometimes returns no image for simple prompts. See
[docs/articles/image-generation.md](docs/articles/image-generation.md).

### Text-to-speech

```csharp
var response = await gemini.GenerateAudioAsync("Have a wonderful day!", new GeminiRequestOptions
{
    VoiceName = "Kore",
    SpeechStyle = "cheerful",
});
var audio = response.GetAudioOrThrow();
await File.WriteAllBytesAsync("greeting.wav", audio.ToWav());
```

Set `VoiceName` for a single voice, or `SpeakerVoices` for a multi-speaker script (write it as
`Joe: Hi! Jane: Hello!` and map each name to a voice). Put delivery directions in `SpeechStyle`, not in
the prompt: the Gemini 3.8 TTS models read the prompt aloud word for word. Google shuts down the older TTS
models on November 17, 2026. See [docs/articles/tts.md](docs/articles/tts.md).

To speak with a custom voice, pass its id (`voice_...`) as `VoiceName`. `IVoiceService` wraps Google's
Voices API. It lists and filters voices, designs a new voice from a written description, and deletes
voices. See the custom voices section of the same guide.

### Speech-to-text

```csharp
var interaction = await transcription.TranscribeAsync(audioBytes, "audio/wav");
Console.WriteLine(interaction.GetTextOrThrow());
```

Set `DiarizationMode = "speaker"` and/or `TimestampGranularities = ["word"]` on `TranscriptionOptions`
for per-word speaker labels and timestamps. Called through Google's Interactions API, not
`generateContent`, since that is the only way to reach the dedicated `gemini-3.5-transcribe` model.
See [docs/articles/transcription.md](docs/articles/transcription.md).

### Embeddings

```csharp
var embedding = await embeddings.EmbedContentAsync(
    "gemini-embedding-001", "Your text",
    new EmbeddingOptions { TaskType = GeminiConstants.EmbeddingTaskTypes.RetrievalDocument });

var batch = await embeddings.BatchEmbedContentAsync("gemini-embedding-001", texts);
```

`gemini-embedding-2` also embeds images, audio, video, and PDFs into the same vector space as text:

```csharp
var embedding = await embeddings.EmbedContentAsync("gemini-embedding-2", imageBytes, "image/jpeg", "A red circle");
```

`gemini-embedding-2` silently ignores `EmbeddingOptions.TaskType` (confirmed live, and it does not
return an error). For that model, put the task instruction in the prompt text. See
[docs/articles/multimodal-embeddings.md](docs/articles/multimodal-embeddings.md).

### Files API & context caching

```csharp
// Upload a file, wait until it's processed, then reference it
var file = await files.UploadFileAsync(bytes, "video/mp4", "clip.mp4");
await files.WaitUntilActiveAsync(file.Name!);

// Cache a large reusable context, then reference it by name to save tokens
var cache = await caching.CreateAsync(new CachedContent
{
    Model = "models/gemini-3.8-flash",
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
builder.Services.AddGeminiChatClient("gemini-3.8-flash");          // registers IChatClient
builder.Services.AddGeminiEmbeddingGenerator("gemini-embedding-001"); // registers IEmbeddingGenerator

// elsewhere:
public class MyService(IChatClient chat)
{
    public Task<ChatResponse> Ask(string q) =>
        chat.GetResponseAsync([new ChatMessage(ChatRole.User, q)]);
}
```

## Observability (OpenTelemetry)

Traces and metrics are emitted via `System.Diagnostics` following the OTel GenAI conventions, with no OpenTelemetry dependency forced on you. Opt in:

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

Failures surface as typed exceptions: `GeminiApiException` (status + parsed error), `GeminiRateLimitException`, `GeminiTimeoutException`, `GeminiSerializationException`, and `GeminiContentException`, all deriving from `GeminiException`.

## Cost governance

Cap what a Gemini integration can spend, and see what it actually spends, without writing your own token-counting and pricing code. We found no other .NET Gemini client that offers this (see the footnote on the comparison table at the top). The budget covers both non-streaming and streaming calls. Without streaming coverage, a runaway loop over `StreamAsync` or `StreamChatAsync` could pass the budget unchecked.

```csharp
builder.Services.AddGemini(options =>
{
    options.Budget = new BudgetOptions
    {
        MaxCostPerDayUsd = 50.00m,     // the primary, always-exact mechanism
        MaxCostPerRequestUsd = 2.00m,  // optional: reject one outsized call before it's sent
    };
});
```

The library converts each response's real token usage to a USD cost and records it as the `gemini.client.cost.usd` OpenTelemetry metric. This includes cached-content and "thinking" tokens, priced by Gemini's billing rules. When today's (UTC) actual spend reaches `MaxCostPerDayUsd`, the next call throws `GeminiBudgetExceededException` before it is sent. The rejected call makes no network round-trip and costs nothing. This is the primary mechanism, and it is exact because it uses real billed usage.

`MaxCostPerRequestUsd` is a secondary check. It estimates the cost of one call before the library sends it. The check makes one extra `CountTokensAsync` round-trip to get an exact input-token count. It skips that round-trip when you do not set `MaxCostPerRequestUsd`. It bounds the output side only when you set `MaxTokens`. If the estimate exceeds the ceiling, the call throws `GeminiRequestCostExceededException`. An estimate cannot be as exact as the daily budget. See [Cost governance](docs/articles/cost-governance.md) for what it can and cannot guarantee, the multi-instance caveat, and pricing overrides.

## Batch API

Submit large volumes of `generateContent` requests for asynchronous processing at Google's discounted batch rate (50% of the standard cost). Two ways in: inline for small jobs, or `CreateFromRequestsFileAsync` for larger ones, which writes and uploads the JSONL file for you so you never hand-roll the format yourself.

```csharp
var job = await batchService.CreateFromRequestsFileAsync(
    "gemini-3.8-flash",
    new List<BatchRequestLine> { new() { Key = "row-1", Request = new GenerateContentRequest { /* ... */ } } });

var finished = await batchService.WaitUntilCompleteAsync(job.Name!, timeout: TimeSpan.FromHours(2));
var results = await batchService.GetResultsAsync(finished);
```

Batch needs a paid-tier Gemini project. Google targets a 24-hour turnaround but gives no hard SLA. Cost governance and rate limiting do not cover batch, because Google gives batch its own quota pool and pricing. See [Batch API](docs/articles/batch-api.md) for details.

## Performance

Auth, retries, rate limiting, cost governance, and telemetry add work to each call. BenchmarkDotNet measured the full pipeline against a bare `HttpClient`, using an in-memory fake handler and no real network. The pipeline adds a few microseconds and a handful of KB per call. A real Gemini call takes 200ms or more, so the overhead is small by comparison. The numbers, method, and caveats live in one place so they do not drift out of sync with a copy here. See [Performance benchmarks](docs/articles/benchmarks.md).

## Documentation & samples

- Guides and API reference: the [`docs/`](docs/) DocFX site. It has guides for getting started, structured output, streaming, resilience, observability, M.E.AI, files and caching, image generation, TTS, speech-to-text, multimodal embeddings, cost governance, the batch API, and performance benchmarks. It also has a v5-to-v6 migration guide. The Docs workflow publishes it to GitHub Pages.
- Runnable sample: [`samples/Junaid.GoogleGemini.Net.AspNetCoreSample`](samples/Junaid.GoogleGemini.Net.AspNetCoreSample), a minimal ASP.NET Core API showing generation, `GenerateAsync<T>`, streaming, `IChatClient`, and OpenTelemetry.
- Benchmarks: [`benchmarks/Junaid.GoogleGemini.Net.Benchmarks`](benchmarks/Junaid.GoogleGemini.Net.Benchmarks), a BenchmarkDotNet project that measures the library's own overhead. See [Performance](#performance) above.

## Requirements

- .NET 8.0, .NET 9.0, or any netstandard2.0 runtime (.NET Framework 4.6.1+, Mono, Unity)
- A Google AI Studio API key

## AI-assisted development

AI develops much of this library, and we want to say so plainly. Claude ([Anthropic's coding agent](https://www.anthropic.com/claude)) carried out the v5 to v6 modernization from start to finish, under the maintainer's direction. That includes the architecture, code, tests, documentation, and this README.

What that means for you:

- An automated test suite and CI run on every commit. The public API is documented and follows [semantic versioning](https://semver.org/).
- 6.0 is stable and was validated live against the Gemini API. Existing 5.x users get it as the default install. As with any dependency, pin a version you have tested for your own use case.
- If you spot something wrong, please [open an issue](https://github.com/jaslam94/Junaid.GoogleGemini.Net/issues).

## Contributing & support

Issues and PRs welcome on [GitHub](https://github.com/jaslam94/Junaid.GoogleGemini.Net). Licensed under MIT. Cutting a release? See [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md).
