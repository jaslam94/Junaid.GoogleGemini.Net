using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services
{
    public class EmbeddingService
    {
        private readonly IGeminiClient GeminiClient;

        public EmbeddingService()
        {
            GeminiClient = GeminiConfiguration.GeminiClient;
        }

        public EmbeddingService(GeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
        }

        public async Task<EmbedContentResponse> EmbedContentAsync(string model, string text)
        {
            EmbedContentRequest request = CreateRequestModel(model, text);
            return await GeminiClient.PostAsync<EmbedContentRequest, EmbedContentResponse>($"/v1beta/models/{model}:embedContent", request);
        }

        public async Task<BatchEmbedContentResponse> BatchEmbedContentAsync(string model, string[] texts)
        {
            BatchEmbedContentRequest request = CreateBatchRequestModel(model, texts);
            return await GeminiClient.PostAsync<BatchEmbedContentRequest, BatchEmbedContentResponse>($"/v1beta/models/{model}:batchEmbedContents", request);
        }

        private static EmbedContentRequest CreateRequestModel(string model, string text)
        {
            return new EmbedContentRequest
            {
                model = $"models/{model}",
                content = new Content
                {
                    parts = new[]
                    {
                        new Part
                        {
                            text = text
                        }
                    }
                }
            };
        }

        private static BatchEmbedContentRequest CreateBatchRequestModel(string model, string[] texts)
        {
            var requests = new List<EmbedContentRequest>();
            foreach (var text in texts)
            {
                requests.Add(new EmbedContentRequest
                {
                    model = $"models/{model}",
                    content = new Content
                    {
                        parts = new[]
                            {
                                new Part
                                {
                                    text = text
                                }
                            }
                    }
                });
            }
            return new BatchEmbedContentRequest { requests = requests.ToArray() };
        }
    }
}