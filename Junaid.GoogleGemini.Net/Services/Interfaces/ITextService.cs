using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    public interface ITextService
    {
        Task<CountTokensResponse> CountTokensAsync(string text);
        Task<GenerateContentResponse> GenereateContentAsync(string text, GenerateContentConfiguration? configuration = null);
        Task StreamGenereateContentAsync(string text, Action<string> handleStreamResponse, GenerateContentConfiguration? configuration = null);
    }
}