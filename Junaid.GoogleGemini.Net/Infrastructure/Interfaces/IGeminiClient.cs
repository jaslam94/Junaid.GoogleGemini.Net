using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Interfaces;

/// <summary>
/// Low-level transport contract for the Gemini API.
/// </summary>
public interface IGeminiClient
{
    /// <summary>Sends a GET request and deserializes the response.</summary>
    Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Sends a POST request with a JSON body and deserializes the response.</summary>
    Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

    /// <summary>Sends a PATCH request with a JSON body and deserializes the response.</summary>
    Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

    /// <summary>Sends a DELETE request.</summary>
    Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a generate-content request, yielding each <see cref="GenerateContentResponse"/> chunk
    /// as it arrives over Server-Sent Events.
    /// </summary>
    IAsyncEnumerable<GenerateContentResponse> StreamAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);
}
