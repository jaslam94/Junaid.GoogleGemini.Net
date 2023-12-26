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
            if (configuration == null)
            {
                return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
            }
            else
            {
                GenerateContentRequestWithConfiguration modelWithConfiguration = CreateRequestModelWithConfiguration(model, configuration);
                return await GeminiClient.PostAsync<GenerateContentRequestWithConfiguration, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", modelWithConfiguration);
            }
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

        private static GenerateContentRequestWithConfiguration CreateRequestModelWithConfiguration(GenerateContentRequest model, GenerateContentConfiguration configuration)
        {
            var modelWithConfiguration = new GenerateContentRequestWithConfiguration
            {
                contents = model.contents,
                safetySettings = new List<object>(),
                generationConfig = configuration.generationConfig
            };

            foreach (var safetySetting in configuration.safetySettings)
            {
                modelWithConfiguration.safetySettings.Add(
                    new
                    {
                        safetySetting.category,
                        safetySetting.threshold
                    });
            }

            return modelWithConfiguration;
        }
    }
}