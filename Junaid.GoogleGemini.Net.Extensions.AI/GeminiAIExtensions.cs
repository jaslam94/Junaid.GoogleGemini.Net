using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Extensions.AI;

/// <summary>
/// DI helpers that register Gemini behind the Microsoft.Extensions.AI abstractions. Call
/// <c>AddGemini(...)</c> from the core package first to register the underlying services.
/// </summary>
public static class GeminiAIExtensions
{
    /// <summary>
    /// Registers an <see cref="IChatClient"/> backed by Gemini. Registered as transient so it resolves
    /// a fresh underlying service (and pooled HttpClient) per use, per HttpClientFactory guidance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="defaultModel">Model id to use when a request doesn't specify one.</param>
    public static IServiceCollection AddGeminiChatClient(this IServiceCollection services, string? defaultModel = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IChatClient>(sp =>
            new GeminiChatClient(sp.GetRequiredService<IGeminiService>(), defaultModel));
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> backed by Gemini embeddings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The embedding model id (e.g. "gemini-embedding-001").</param>
    public static IServiceCollection AddGeminiEmbeddingGenerator(this IServiceCollection services, string model)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            new GeminiEmbeddingGenerator(sp.GetRequiredService<IEmbeddingService>(), model));
        return services;
    }
}
