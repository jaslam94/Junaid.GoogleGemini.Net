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

        public async Task<GenerateContentResponse> GenereateContentAsync(string text, GenerateContentConfiguration configuration = null)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            if (configuration != null)
            {
                ApplyConfiguration(model, configuration);
            }
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

        private static void ApplyConfiguration(GenerateContentRequest model, GenerateContentConfiguration configuration)
        {
            if (configuration.safetySettings != null)
            {
                model.safetySettings = new List<object>();
                foreach (var safetySetting in configuration.safetySettings)
                {
                    model.safetySettings.Add(
                        new
                        {
                            safetySetting.category,
                            safetySetting.threshold
                        });
                }
            }

            model.generationConfig = configuration.generationConfig;
        }
    }
}