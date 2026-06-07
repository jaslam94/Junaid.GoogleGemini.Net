# Streaming

Streaming uses Server-Sent Events under the hood and is surfaced as `IAsyncEnumerable`.

```csharp
await foreach (var chunk in gemini.StreamAsync("Tell me a long story"))
{
    Console.Write(chunk.Text());
}
```

Each `chunk` is a full `GenerateContentResponse`, so you also get `FinishReason`, `Usage`, and any
grounding metadata on the final chunks.

## Callback overload

For simple cases there's a callback overload:

```csharp
await gemini.StreamAsync("Tell me a long story", text => Console.Write(text));
```

## Cancellation

Pass a `CancellationToken`; it cancels the underlying HTTP read promptly:

```csharp
await foreach (var chunk in gemini.StreamAsync(prompt, cancellationToken: ct))
{
    ...
}
```

## In ASP.NET Core

Return the `IAsyncEnumerable` and the framework streams it:

```csharp
app.MapGet("/stream", (IGeminiService gemini, string prompt, CancellationToken ct)
    => Stream(gemini, prompt, ct));

static async IAsyncEnumerable<string> Stream(
    IGeminiService gemini, string prompt, [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var chunk in gemini.StreamAsync(prompt, cancellationToken: ct))
        yield return chunk.Text();
}
```
