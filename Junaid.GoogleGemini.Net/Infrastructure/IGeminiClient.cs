namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public interface IGeminiClient
    {
        string ApiKey { get; }

        HttpClient HttpClient { get; }

        Task<TResponse> GetAsync<TResponse>(string endpoint);

        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);

        IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data);
    }
}