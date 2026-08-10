# Migrating from v5 to v6

v6 is a modernization release with intentional breaking changes. The fixes below address real
correctness bugs in v5 (retries that never worked, fragile streaming), so the upgrade is worth it.

## Breaking changes

### Response models are PascalCase
Wire DTO members are now idiomatic PascalCase.

```csharp
// v5
var n = tokens.totalTokens;
var c = response.candidates[0];

// v6
var n = tokens.TotalTokens;
var c = response.Candidates![0];
```

### Streaming returns `IAsyncEnumerable`
```csharp
// v5
await gemini.StreamAsync(prompt, chunk => Console.Write(chunk));

// v6: primary API
await foreach (var chunk in gemini.StreamAsync(prompt))
    Console.Write(chunk.Text());

// v6: the callback overload still exists
await gemini.StreamAsync(prompt, text => Console.Write(text));
```

### `Text()` no longer returns placeholder strings
It returns the text, or an **empty string** when there is none (instead of sentences like
`"[Content was blocked...]"`). Use `TryGetText` / `GetTextOrThrow` to detect missing content, and
`FinishReason` / `BlockReason` to see why.

### Typed exceptions
`GeminiException` is now the base type. Status code and parsed error moved to `GeminiApiException`.

```csharp
// v5
catch (GeminiException ex) { var code = ex.StatusCode; }

// v6
catch (GeminiApiException ex) { var code = ex.StatusCode; var status = ex.Status; }
```

### Defaults
- The client now targets the **v1beta** API by default (unlocks structured output, thinking,
  grounding, caching, the Files API).
- Deprecated model fallbacks (e.g. `gemini-1.5-pro`) were removed; vision uses your default model.
- The **default model is now `gemini-3.6-flash`** (a current GA model). Set `DefaultModel` /
  `options.Model` to pin a specific model.
- **Model names are no longer validated against an allow-list**, so any current or future model
  (for example the Gemini 3 family) works without a library update; the API rejects genuinely invalid names.
- The **default request timeout is now 100s** (was 30s). Gemini 3 "thinking" models can take well
  over a minute; lower `TimeoutSeconds` or set a lower `ThinkingLevel` for latency-sensitive work.
- **No default temperature is forced** anymore. If you don't set `Temperature`, the model uses its
  own default (1.0 on Gemini 3, which advises against lowering it).

### .NET Framework
The `netstandard2.0` build runs on .NET Framework 4.6.1+. Enable
`<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>` in your app; the required
`System.ComponentModel.Annotations` now flows in transitively.

## New in v6

- `GenerateAsync<T>` structured output
- System instructions, thinking config, JSON mode
- Tools: Google Search grounding, URL context, code execution; function declarations
- Files API and context caching
- OpenTelemetry traces & metrics
- `Microsoft.Extensions.AI` adapters (`IChatClient`, `IEmbeddingGenerator`)
- `netstandard2.0` target

### Gemini 3
- `ThinkingLevel` (`minimal`/`low`/`medium`/`high`), Gemini 3's reasoning control, mutually
  exclusive with the 2.5-era `ThinkingBudget`.
- `MediaResolution` for image/video/PDF parts.
- The model's encrypted `thoughtSignature` is captured on response parts and can be **echoed back
  for multi-turn function calling** via the new `Content`-based `ChatAsync`/`StreamChatAsync`
  overloads (Gemini 3 returns HTTP 400 if a function-call signature isn't replayed).
