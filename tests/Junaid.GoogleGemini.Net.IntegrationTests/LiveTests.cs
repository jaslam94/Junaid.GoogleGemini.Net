using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests against the real Gemini API. Assertions check structure/contract (non-empty output,
/// token counts, types, status codes) rather than exact text, since model output is non-deterministic.
/// All tests skip unless GeminiApiKey is set. Uses gemini-2.5-flash to keep cost/latency low.
/// </summary>
[Collection("Live")]
public class LiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task GenerateAsync_ReturnsText()
    {
        var response = await Gemini.GenerateAsync("Reply with exactly the word: pong");

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
        Assert.Equal("STOP", response.FinishReason);
        Assert.True(response.Usage?.TotalTokenCount > 0);
    }

    [RequiresGeminiKey]
    public async Task GenerateAsyncT_ReturnsTypedObject()
    {
        var book = await Gemini.GenerateAsync<Book>("Return one well-known classic novel.");

        Assert.False(string.IsNullOrWhiteSpace(book.Title));
        Assert.False(string.IsNullOrWhiteSpace(book.Author));
    }

    [RequiresGeminiKey]
    public async Task StreamAsync_YieldsChunks()
    {
        var chunks = new List<string>();
        await foreach (var chunk in Gemini.StreamAsync("Count from 1 to 5, one number per line."))
        {
            chunks.Add(chunk.Text());
        }

        Assert.NotEmpty(chunks);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(chunks)));
    }

    [RequiresGeminiKey]
    public async Task CountTokensAsync_ReturnsPositive()
    {
        var tokens = await Gemini.CountTokensAsync("Hello, world.");

        Assert.True(tokens.TotalTokens > 0);
    }

    [RequiresGeminiKey]
    public async Task EmbedContent_ReturnsVector()
    {
        var embeddings = fixture.Get<IEmbeddingService>();

        var result = await embeddings.EmbedContentAsync("gemini-embedding-001", "semantic search text");

        Assert.NotNull(result.Embedding?.Values);
        Assert.True(result.Embedding!.Values!.Length > 0);
    }

    [RequiresGeminiKey]
    public async Task ListModels_ReturnsModels()
    {
        var models = await fixture.Get<IModelInfoService>().ListModelsAsync();

        Assert.NotEmpty(models.Models);
    }

    [RequiresGeminiKey]
    public async Task GetModel_Unknown_ThrowsApiException()
    {
        // Passes client-side validation (length only), so this reaches the API and returns a 4xx —
        // verifying our error mapping against the real service.
        var ex = await Assert.ThrowsAsync<GeminiApiException>(
            () => fixture.Get<IModelInfoService>().GetModelAsync("gemini-does-not-exist-9999"));

        Assert.True((int)ex.StatusCode >= 400);
    }

    [RequiresGeminiKey]
    public async Task ChatClient_GetResponseAsync_ReturnsText()
    {
        var chat = fixture.Get<IChatClient>();

        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "Reply with: hi")]);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    private sealed record Book(string Title, string Author, int Year);
}
