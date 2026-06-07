# Migrating from v5 to v6

v6 is a modernization release with intentional breaking changes. The fixes below address real
correctness bugs in v5 (retries that never worked, fragile streaming) — the upgrade is worth it.

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

// v6 — primary API
await foreach (var chunk in gemini.StreamAsync(prompt))
    Console.Write(chunk.Text());

// v6 — the callback overload still exists
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

## New in v6

- `GenerateAsync<T>` structured output
- System instructions, thinking config, JSON mode
- Tools: Google Search grounding, URL context, code execution; function declarations
- Files API and context caching
- OpenTelemetry traces & metrics
- `Microsoft.Extensions.AI` adapters (`IChatClient`, `IEmbeddingGenerator`)
- `netstandard2.0` target
