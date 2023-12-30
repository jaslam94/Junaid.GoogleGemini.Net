using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Helpers;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services
{
    public class VisionService
    {
        private readonly IGeminiClient GeminiClient;

        public VisionService()
        {
            GeminiClient = GeminiConfiguration.GeminiClient;
        }

        public VisionService(GeminiClient geminiClient)
        {
            GeminiClient = geminiClient;
        }

        public async Task<GenerateContentResponse> GenereateContentAsync(string text,
                                                                         FileObject fileObject,
                                                                         GenerateContentConfiguration configuration = null)
        {
            GenerateContentRequest model = CreateRequestModel(text, fileObject);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro-vision:generateContent", model);
        }

        public async Task StreamGenereateContentAsync(string text,
                                                      FileObject fileObject,
                                                      Action<string> handleStreamResponse,
                                                      GenerateContentConfiguration configuration = null)
        {
            GenerateContentRequest model = CreateRequestModel(text, fileObject);
            if (configuration != null)
            {
                model.ApplyConfiguration(configuration);
            }
            await foreach (var data in GeminiClient.PostAsync($"/v1beta/models/gemini-pro-vision:streamGenerateContent", model))
            {
                handleStreamResponse(data);
            }
        }

        public async Task<CountTokensResponse> CountTokensAsync(string text,
                                                                FileObject fileObject)
        {
            GenerateContentRequest model = CreateRequestModel(text, fileObject);
            return await GeminiClient.PostAsync<GenerateContentRequest, CountTokensResponse>($"/v1beta/models/gemini-pro-vision:countTokens", model);
        }

        private static GenerateContentRequest CreateRequestModel(string text, FileObject fileObject)
        {
            var contents = new[]
            {
                new Content
                {
                    parts = new[]
                    {
                        new Part
                        {
                            text = text
                        },
                        new Part
                        {
                            inline_data = new Inline_Data
                            {
                                mime_type = MimeTypeHelper.GetMimeType(fileObject.FileName),
                                data = Convert.ToBase64String(fileObject.FileContent)
                            }
                        }
                    }
                }
            };
            return new GenerateContentRequest(contents);
        }
    }
}