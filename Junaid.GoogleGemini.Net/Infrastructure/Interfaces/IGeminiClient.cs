namespace Junaid.GoogleGemini.Net.Infrastructure.Interfaces;

public interface IGeminiClient
{
    Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default);

    Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken = default);
}