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

    /// <summary>
    /// Sends a request to the Interactions API (<c>POST /v1beta/interactions</c>). Not a thin wrapper
    /// around <see cref="PostAsync{TRequest, TResponse}"/>: the Interactions API's error body shape
    /// differs (a string <c>code</c>, not the numeric one <see cref="PostAsync{TRequest, TResponse}"/>
    /// expects) and its model name lives in the request body, not the URL, so telemetry tagging needs
    /// it passed explicitly. See <c>PLAN-stt.md</c> §3.4/§5.3.
    /// </summary>
    Task<Interaction> PostInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a previously created interaction by ID (<c>GET /v1beta/interactions/{id}</c>).</summary>
    Task<Interaction> GetInteractionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a request to the Interactions API, yielding each named SSE event as it arrives. A
    /// different protocol than <see cref="StreamAsync{TRequest}"/>: see <see cref="InteractionStreamEvent"/>.
    /// </summary>
    IAsyncEnumerable<InteractionStreamEvent> StreamInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default);
}
