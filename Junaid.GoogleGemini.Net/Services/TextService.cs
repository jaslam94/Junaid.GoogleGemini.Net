﻿using Junaid.GoogleGemini.Net.Infrastructure;
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

        public TextService(GeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(string text,
                                                                         GenerateContentConfiguration configuration = null)
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
                                                      GenerateContentConfiguration configuration = null)
        {
            GenerateContentRequest model = CreateRequestModel(text);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            await foreach (var data in GeminiClient.PostAsync($"/v1beta/models/gemini-pro:streamGenerateContent", model))
            {
                handleStreamResponse(data);
            }
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