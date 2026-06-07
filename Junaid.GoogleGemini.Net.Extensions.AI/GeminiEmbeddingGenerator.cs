using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using GeminiEmbedding = Junaid.GoogleGemini.Net.Models.GoogleApi.Embedding;

namespace Junaid.GoogleGemini.Net.Extensions.AI;

/// <summary>
/// Adapts <see cref="IEmbeddingService"/> to <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> so
/// Gemini embeddings work with the Microsoft.Extensions.AI abstractions (vector stores, RAG pipelines).
/// </summary>
public sealed class GeminiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IEmbeddingService _service;
    private readonly string _model;

    /// <summary>Creates an embedding generator bound to a specific embedding model.</summary>
    /// <param name="service">The underlying embedding service.</param>
    /// <param name="model">The embedding model id (e.g. "gemini-embedding-001").</param>
    public GeminiEmbeddingGenerator(IEmbeddingService service, string model)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("An embedding model id is required.", nameof(model));
        }
        _model = model;
    }

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.ToList();
        var model = options?.ModelId ?? _model;
        var embeddingOptions = options?.Dimensions is { } dims
            ? new EmbeddingOptions { OutputDimensionality = dims }
            : null;

        var results = new GeneratedEmbeddings<Embedding<float>>();

        if (inputs.Count == 0)
        {
            return results;
        }

        if (inputs.Count == 1)
        {
            var single = await _service.EmbedContentAsync(model, inputs[0], embeddingOptions, cancellationToken);
            results.Add(ToEmbedding(single.Embedding, model));
            return results;
        }

        var batch = await _service.BatchEmbedContentAsync(model, inputs.ToArray(), embeddingOptions, cancellationToken);
        foreach (var embedding in batch.Embeddings ?? [])
        {
            results.Add(ToEmbedding(embedding, model));
        }

        return results;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The underlying service/HTTP lifetime is owned by DI.
    }

    private static Embedding<float> ToEmbedding(GeminiEmbedding? source, string model)
    {
        var vector = source?.Values ?? [];
        return new Embedding<float>(vector) { ModelId = model };
    }
}
