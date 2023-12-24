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
            var model = new GenerateContentRequest
            {
                contents = new Content[]
                {
                    new Content
                    {
                        parts = new Part[]
                        {
                            new Part
                            {
                                text = text
                            }
                        }
                    }
                }
            };

            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
        }
    }
}