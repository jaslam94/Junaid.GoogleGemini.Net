using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Constants;
using Junaid.GoogleGemini.Net.Infrastructure.Helpers;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for creating Gemini API requests
    /// </summary>
    public static class GeminiRequestExtensions
    {
        /// <summary>
        /// Creates a base request builder with common settings
        /// </summary>
        public static ContentRequestBuilder CreateRequest(this IGeminiClient client)
        {
            return new ContentRequestBuilder()
                .WithSafetySettings(GetDefaultSafetySettings());
        }

        /// <summary>
        /// Creates a text request with default settings
        /// </summary>
        public static ContentRequestBuilder CreateTextRequest(this IGeminiClient client, string text)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText(text)
                .AddMessage()
                .WithTemperature(0.7f);
        }

        /// <summary>
        /// Creates a vision request for processing text and image
        /// </summary>
        public static ContentRequestBuilder CreateVisionRequest(
            this IGeminiClient client,
            string text,
            FileObject fileObject)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText(text)
                .AddImage(
                    Convert.ToBase64String(fileObject.FileContent),
                    MimeTypeHelper.GetMimeType(fileObject.FileName))
                .AddMessage()
                .WithTemperature(0.7f);
        }

        /// <summary>
        /// Creates a chat request with message history
        /// </summary>
        public static ContentRequestBuilder CreateChatRequest(
            this IGeminiClient client,
            MessageObject[] messages)
        {
            var builder = client.CreateRequest();

            foreach (var message in messages)
            {
                builder
                    .WithRole(message.Role)
                    .AddText(message.Text)
                    .AddMessage();
            }

            return builder.WithTemperature(0.7f);
        }

        /// <summary>
        /// Creates a code generation request
        /// </summary>
        public static ContentRequestBuilder CreateCodeRequest(
            this IGeminiClient client,
            string prompt,
            string language)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText($"Generate {language} code for: {prompt}\n" +
                        $"Only provide the code without any explanations. " +
                        $"Make sure the code follows best practices and includes proper error handling.")
                .AddMessage()
                .WithTemperature(0.1f); // Lower temperature for precise code generation
        }

        /// <summary>
        /// Creates an embedding request
        /// </summary>
        public static ContentRequestBuilder CreateEmbeddingRequest(
            this IGeminiClient client,
            string text)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText(text)
                .AddMessage();
        }

        /// <summary>
        /// Creates a request with streaming enabled
        /// </summary>
        public static ContentRequestBuilder CreateStreamingRequest(
            this IGeminiClient client,
            ContentRequestBuilder builder)
        {
            return builder.EnableStreaming(true);
        }

        /// <summary>
        /// Creates a request optimized for creative tasks
        /// </summary>
        public static ContentRequestBuilder CreateCreativeRequest(
            this IGeminiClient client,
            string prompt)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText(prompt)
                .AddMessage()
                .WithTemperature(0.9f)
                .WithTopP(0.8f)
                .WithTopK(40);
        }

        /// <summary>
        /// Creates a request optimized for factual responses
        /// </summary>
        public static ContentRequestBuilder CreateFactualRequest(
            this IGeminiClient client,
            string prompt)
        {
            return client.CreateRequest()
                .WithRole("user")
                .AddText(prompt)
                .AddMessage()
                .WithTemperature(0.1f)
                .WithTopP(0.1f)
                .WithTopK(1);
        }

        private static List<SafetySetting> GetDefaultSafetySettings()
        {
            return new List<SafetySetting>
            {
                new() { Category = SafetyCategory.Harassment, Threshold = SafetyThreshold.Medium },
                new() { Category = SafetyCategory.HateSpeech, Threshold = SafetyThreshold.Medium },
                new() { Category = SafetyCategory.SexuallyExplicit, Threshold = SafetyThreshold.High },
                new() { Category = SafetyCategory.DangerousContent, Threshold = SafetyThreshold.Medium }
            };
        }
    }
}
