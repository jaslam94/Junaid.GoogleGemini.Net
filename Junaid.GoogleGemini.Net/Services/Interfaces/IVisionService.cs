using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    public interface IVisionService
    {
        Task<CountTokensResponse> CountTokensAsync(string text, FileObject fileObject);
        Task<GenerateContentResponse> GenereateContentAsync(string text, FileObject fileObject, GenerateContentConfiguration configuration = null);
        Task StreamGenereateContentAsync(string text, FileObject fileObject, Action<string> handleStreamResponse, GenerateContentConfiguration configuration = null);
    }
}