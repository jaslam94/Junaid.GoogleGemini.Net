using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;

namespace Junaid.GoogleGemini.Net.Services
{
    public class ChatService : Service, IChatService
    {
        public ChatService(GeminiClient geminiClient) : base(geminiClient)
        {
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(MessageObject[] chat,
                                                                         GenerateContentConfiguration? configuration)
        {
            GenerateContentRequest model = CreateRequestModel(chat);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro:generateContent", model);
        }

        public async Task StreamGenereateContentAsync(MessageObject[] chat,
                                                      Action<string> handleStreamResponse,
                                                      GenerateContentConfiguration? configuration)
        {
            GenerateContentRequest model = CreateRequestModel(chat);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            await foreach (var data in GeminiClient.SendAsync($"/v1beta/models/gemini-pro:streamGenerateContent", model))
            {
                handleStreamResponse(data);
            }
        }

        public async Task<CountTokensResponse> CountTokensAsync(MessageObject[] chat)
        {
            GenerateContentRequest model = CreateRequestModel(chat);
            return await GeminiClient.PostAsync<GenerateContentRequest, CountTokensResponse>($"/v1beta/models/gemini-pro:countTokens", model);
        }

        private static GenerateContentRequest CreateRequestModel(MessageObject[] chat)
        {
            var contents = new List<Content>();

            foreach (var message in chat)
            {
                contents.Add(new Content
                {
                    role = message.Role,
                    parts = new[]
                    {
                        new Part
                        {
                            text = message.Text
                        }
                    }
                });
            }

            return new GenerateContentRequest(contents.ToArray());
        }
    }
}