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