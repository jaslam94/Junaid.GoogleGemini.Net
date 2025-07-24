using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Interfaces
{
    public interface IGeminiClient
    {
        Task<TResponse> GetAsync<TResponse>(string endpoint);
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data);
    }
}
