using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<BatchEmbedContentResponse> BatchEmbedContentAsync(string model, string[] texts);
        Task<EmbedContentResponse> EmbedContentAsync(string model, string text);
    }
}