using Junaid.GoogleGemini.Net.Infrastructure.Constants;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Factories
{
    /// <summary>
    /// Factory for creating simple Gemini API requests without builder pattern overhead
    /// </summary>
    public static class RequestFactory
    {
        /// <summary>
        /// Creates a simple text request
        /// </summary>
        public static GenerateContentRequest CreateTextRequest(string text, GeminiRequestOptions? options = null)
        {
            var content = new Content
            {
                Role = "user",
                Parts = new List<Part> { new() { Text = text } }
            };

            return new GenerateContentRequest
            {
                Contents = new List<Content> { content },
                GenerationConfig = CreateGenerationConfig(options),
                SafetySettings = options?.SafetySettings ?? GetDefaultSafetySettings()
            };
        }

        /// <summary>
        /// Creates a simple vision request (text + image)
        /// </summary>
        public static GenerateContentRequest CreateVisionRequest(
            string text, 
            FileObject image, 
            GeminiRequestOptions? options = null,
            bool streaming = false)
        {
            var parts = new List<Part>
            {
                new() { Text = text },
                new() 
                { 
                    InlineData = new InlineData
                    {
                        Data = Convert.ToBase64String(image.FileContent),
                        MimeType = FileUtilities.GetMimeType(image.FileName)
                    }
                }
            };

            var content = new Content
            {
                Role = "user",
                Parts = parts
            };

            return new GenerateContentRequest
            {
                Contents = new List<Content> { content },
                GenerationConfig = CreateGenerationConfig(options),
                SafetySettings = options?.SafetySettings ?? ConfigurationUtilities.CreateSafetySettings(ConfigurationUtilities.GetDefaultSafetyThresholds())
            };
        }

        /// <summary>
        /// Creates a simple chat request from message history
        /// </summary>
        public static GenerateContentRequest CreateChatRequest(
            MessageObject[] messages, 
            GeminiRequestOptions? options = null)
        {
            var contents = messages.Select(msg => new Content
            {
                Role = msg.Role,
                Parts = new List<Part> { new() { Text = msg.Text } }
            }).ToList();

            return new GenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = CreateGenerationConfig(options),
                SafetySettings = options?.SafetySettings ?? GetDefaultSafetySettings()
            };
        }

        /// <summary>
        /// Creates generation config from options
        /// </summary>
        private static GenerationConfig CreateGenerationConfig(GeminiRequestOptions? options)
        {
            if (options == null)
            {
                return new GenerationConfig { Temperature = GeminiConstants.Defaults.Temperature };
            }

            return new GenerationConfig
            {
                Temperature = options.Temperature,
                MaxOutputTokens = options.MaxTokens,
                TopP = options.TopP,
                TopK = options.TopK,
                StopSequences = options.StopSequences
            };
        }

        /// <summary>
        /// Gets default safety settings
        /// </summary>
        private static List<SafetySetting> GetDefaultSafetySettings()
        {
            return ConfigurationUtilities.CreateSafetySettings(ConfigurationUtilities.GetDefaultSafetyThresholds());
        }
    }
}