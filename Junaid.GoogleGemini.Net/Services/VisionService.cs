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

        public async Task<GenerateContentResponse> GenereateContentAsync(string text, FileObject fileObject)
        {
            GenerateContentRequest model = CreateRequestModel(text, fileObject);
            return await GeminiClient.PostAsync<GenerateContentRequest, GenerateContentResponse>($"/v1beta/models/gemini-pro-vision:generateContent", model);
        }

        private static GenerateContentRequest CreateRequestModel(string text, FileObject fileObject)
        {
            return new GenerateContentRequest
            {
                contents = new[]
                {
                    new Content
                    {
                        parts = new List<object>
                        {
                            new 
                            {
                                text = text
                            },
                            new 
                            {
                                inline_data = new Inline_Data
                                {
                                    mime_type = MimeTypeHelper.GetMimeType(fileObject.FileName),
                                    data = Convert.ToBase64String(fileObject.FileContent)
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}