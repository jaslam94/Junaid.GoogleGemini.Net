using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services
{
    public class TextService
    {
        private readonly IGeminiClient GeminiClient;

        public TextService()
        {
            GeminiClient = GeminiConfiguration.GeminiClient;
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(string text)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
        }

        private static GenerateContentRequest CreateRequestModel(string text)
        {
            return new GenerateContentRequest
            {
                contents = new Content[]
                {
                    new Content
                    {
                        parts = new List<object>
                        {
                            new
                            {
                                text = text
                            }
                        }
                    }
                }
            };
        }
    }
}