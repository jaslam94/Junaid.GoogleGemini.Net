using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    public class TextService : Service, ITextService
    {
        public TextService(IGeminiClient geminiClient) : base(geminiClient)
        {
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(string text,
                                                                         GenerateContentConfiguration? configuration)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
        }

        public async Task StreamGenereateContentAsync(string text,
                                                      Action<string> handleStreamResponse,
                                                      GenerateContentConfiguration? configuration)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            await foreach (var data in GeminiClient.SendAsync($"/v1beta/models/gemini-pro:streamGenerateContent", model))
            {
                handleStreamResponse(data);
            }
        }

        public async Task<CountTokensResponse> CountTokensAsync(string text)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            return await GeminiClient.PostAsync<GenerateContentRequest, CountTokensResponse>($"/v1beta/models/gemini-pro:countTokens", model);
        }

        private static GenerateContentRequest CreateRequestModel(string text)
        {
            var contents = new Content[]
            {
                new Content
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
            return new GenerateContentRequest(contents);
        }
    }
}