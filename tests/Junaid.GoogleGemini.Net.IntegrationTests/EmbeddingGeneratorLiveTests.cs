using Microsoft.Extensions.AI;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests for <see cref="Extensions.AI.GeminiEmbeddingGenerator"/> -- the
/// Microsoft.Extensions.AI <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> adapter. Previously
/// only <see cref="IChatClient"/> was live-tested (<see cref="LiveTests.ChatClient_GetResponseAsync_ReturnsText"/>);
/// the embedding adapter had no live coverage of its own, including its single-vs-batch code path split.
/// </summary>
[Collection("Live")]
public class EmbeddingGeneratorLiveTests(GeminiFixture fixture)
{
    private IEmbeddingGenerator<string, Embedding<float>> Generator =>
        fixture.Get<IEmbeddingGenerator<string, Embedding<float>>>();

    [RequiresGeminiKey]
    public async Task GenerateAsync_SingleInput_ReturnsVector()
    {
        var result = await Generator.GenerateAsync(["semantic search text"]);

        Assert.Single(result);
        Assert.NotEmpty(result[0].Vector.ToArray());
    }

    [RequiresGeminiKey]
    public async Task GenerateAsync_MultipleInputs_UsesBatchPathAndReturnsDistinctVectors()
    {
        // 2+ distinct inputs exercises GeminiEmbeddingGenerator's BatchEmbedContentAsync branch,
        // distinct from the single-input path above. NOTE: can't include a duplicate text here to
        // prove per-index correlation -- EmbeddingService.ValidateNoDuplicates (a pre-existing,
        // intentional guard in the base IEmbeddingService, not something this test triggers by
        // accident) rejects any batch containing repeated text with an ArgumentException. That
        // restriction is inherited transparently by this Microsoft.Extensions.AI adapter, even though
        // IEmbeddingGenerator<TInput,TEmbedding>'s own contract doesn't document "inputs must be
        // unique" -- worth knowing if a RAG pipeline ever batches naturally-repeated chunks.
        var result = await Generator.GenerateAsync(["a cat sits on a mat", "quarterly financial report", "the stock market rose today"]);

        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.NotEmpty(e.Vector.ToArray()));
        Assert.NotEqual(result[0].Vector.ToArray(), result[1].Vector.ToArray());
        Assert.NotEqual(result[1].Vector.ToArray(), result[2].Vector.ToArray());
    }

    [RequiresGeminiKey]
    public async Task GenerateAsync_WithDimensions_HonorsRequestedOutputDimensionality()
    {
        var result = await Generator.GenerateAsync(
            ["dimensionality reduction test"],
            new EmbeddingGenerationOptions { Dimensions = 256 });

        Assert.Single(result);
        Assert.Equal(256, result[0].Vector.Length);
    }
}
