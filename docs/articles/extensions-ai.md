# Microsoft.Extensions.AI integration

The companion package `Junaid.GoogleGemini.Net.Extensions.AI` adapts Gemini to the standard .NET AI
abstractions, so it works with Semantic Kernel, agent frameworks, and any middleware built on
`IChatClient` / `IEmbeddingGenerator`.

```shell
dotnet add package Junaid.GoogleGemini.Net.Extensions.AI
```

## Register

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));
builder.Services.AddGeminiChatClient("gemini-2.5-flash");            // IChatClient
builder.Services.AddGeminiEmbeddingGenerator("gemini-embedding-001"); // IEmbeddingGenerator
```

## Use IChatClient

```csharp
public class Assistant(IChatClient chat)
{
    public async Task<string> Ask(string question)
    {
        var response = await chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, question)]);
        return response.Text;
    }
}
```

Streaming works through the same abstraction:

```csharp
await foreach (var update in chat.GetStreamingResponseAsync(messages))
    Console.Write(update.Text);
```

## Use IEmbeddingGenerator

```csharp
public class Indexer(IEmbeddingGenerator<string, Embedding<float>> embeddings)
{
    public async Task<ReadOnlyMemory<float>> Embed(string text)
    {
        var result = await embeddings.GenerateAsync([text]);
        return result[0].Vector;
    }
}
```

System messages, `ChatOptions` (model, temperature, max tokens, stop sequences, seed), finish reasons,
and token usage are mapped across automatically.
