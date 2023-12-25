﻿using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services
{
    public class ChatService
    {
        private readonly IGeminiClient GeminiClient;

        public ChatService()
        {
            GeminiClient = GeminiConfiguration.GeminiClient;
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(MessageObject[] chat)
        {
            GenerateContentRequest model = CreateRequestModel(chat);
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
        }

        private static GenerateContentRequest CreateRequestModel(MessageObject[] chat)
        {
            var contents = new List<Content>();

            foreach (var message in chat)
            {
                contents.Add(new Content
                {
                    role = message.Role,
                    parts = new List<object>
                    {
                        new
                        {
                            text = message.Text
                        }
                    }
                });
            }

            return new GenerateContentRequest
            {
                contents = contents.ToArray()
            };
        }
    }
}