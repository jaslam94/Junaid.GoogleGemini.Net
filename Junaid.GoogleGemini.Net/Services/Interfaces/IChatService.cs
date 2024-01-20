using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    public interface IChatService
    {
        Task<CountTokensResponse> CountTokensAsync(MessageObject[] chat);
        Task<GenerateContentResponse> GenereateContentAsync(MessageObject[] chat, GenerateContentConfiguration configuration = null);
        Task StreamGenereateContentAsync(MessageObject[] chat, Action<string> handleStreamResponse, GenerateContentConfiguration configuration = null);
    }
}