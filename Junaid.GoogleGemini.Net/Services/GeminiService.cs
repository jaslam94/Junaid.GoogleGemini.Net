using Junaid.GoogleGemini.Net.Infrastructure.Builders;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Unified service implementation for all Gemini content generation operations
    /// </summary>
    public class GeminiService(
        IGeminiClient geminiClient,
        ILogger<GeminiService> logger,
        IOptions<GeminiOptions> options,
        ISafetyService safetyService) : Service(geminiClient, logger, options, safetyService), IGeminiService
    {
        private static readonly string[] SupportedImageTypes = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenerateAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);

            var request = CreateTextRequest(prompt, options);
            var endpoint = GetGenerateEndpoint(options?.Model);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "text generation",
                endpoint,
                request,
                new { PromptLength = prompt.Length, Model = options?.Model ?? Options?.DefaultModel },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> GenerateWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(image, nameof(image));

            var request = CreateVisionRequest(prompt, image, options);
            var endpoint = GetGenerateEndpoint(options?.Model ?? GeminiConstants.Models.Gemini15Pro);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "vision generation",
                endpoint,
                request,
                new { PromptLength = prompt.Length, ImageSize = image.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));

            var request = CreateChatRequest(messages, options);
            var endpoint = GetGenerateEndpoint(options?.Model);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "chat generation",
                endpoint,
                request,
                new { MessageCount = messages.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamAsync(
            string prompt,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            var request = CreateTextRequest(prompt, options, streaming: true);
            var endpoint = GetStreamEndpoint(options?.Model);

            await ExecuteStreamRequestAsync(
                "text streaming",
                endpoint,
                request,
                handleResponse,
                new { PromptLength = prompt.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamWithImageAsync(
            string prompt,
            FileObject image,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(image, nameof(image));
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            var request = CreateVisionRequest(prompt, image, options, streaming: true);
            var endpoint = GetStreamEndpoint(options?.Model ?? GeminiConstants.Models.Gemini15Pro);

            await ExecuteStreamRequestAsync(
                "vision streaming",
                endpoint,
                request,
                handleResponse,
                new { PromptLength = prompt.Length, ImageSize = image.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task StreamChatAsync(
            MessageObject[] messages,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            var request = CreateChatRequest(messages, options, streaming: true);
            var endpoint = GetStreamEndpoint(options?.Model);

            await ExecuteStreamRequestAsync(
                "chat streaming",
                endpoint,
                request,
                handleResponse,
                new { MessageCount = messages.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);

            var request = RequestFactory.CreateTokenCountingTextRequest(prompt);
            var endpoint = GetCountTokensEndpoint(null);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "token counting",
                endpoint,
                request,
                new { PromptLength = prompt.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(image, nameof(image));

            var request = RequestFactory.CreateTokenCountingVisionRequest(prompt, image);
            var endpoint = GetCountTokensEndpoint(GeminiConstants.Models.Gemini15Pro);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "vision token counting",
                endpoint,
                request,
                new { PromptLength = prompt.Length, ImageSize = image.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));

            var request = RequestFactory.CreateTokenCountingChatRequest(messages);
            var endpoint = GetCountTokensEndpoint(null);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "chat token counting",
                endpoint,
                request,
                new { MessageCount = messages.Length },
                cancellationToken);
        }

        #region Private Helper Methods

        private static GenerateContentRequest CreateTextRequest(string prompt, GeminiRequestOptions? options, bool streaming = false)
        {
            var request = RequestFactory.CreateTextRequest(prompt, options);

            if (streaming)
            {
                // For streaming, we need the builder
                var builder = new ContentRequestBuilder()
                    .WithRole("user")
                    .AddText(prompt)
                    .AddMessage()
                    .EnableStreaming(true);

                ApplyOptionsToBuilder(builder, options);
                return builder.Build();
            }

            return request;
        }

        private static GenerateContentRequest CreateVisionRequest(string prompt, FileObject image, GeminiRequestOptions? options, bool streaming = false)
        {
            if (!streaming)
            {
                return RequestFactory.CreateVisionRequest(prompt, image, options);
            }

            // For streaming, we need the builder
            var builder = new ContentRequestBuilder()
                .WithRole("user")
                .AddText(prompt)
                .AddImage(
                    Convert.ToBase64String(image.FileContent),
                    FileUtilities.GetMimeType(image.FileName))
                .AddMessage()
                .EnableStreaming(true);

            ApplyOptionsToBuilder(builder, options);
            return builder.Build();
        }

        private static GenerateContentRequest CreateChatRequest(MessageObject[] messages, GeminiRequestOptions? options, bool streaming = false)
        {
            if (!streaming)
            {
                return RequestFactory.CreateChatRequest(messages, options);
            }

            // For streaming, we need the builder
            var builder = new ContentRequestBuilder();
            foreach (var message in messages)
            {
                builder
                    .WithRole(message.Role)
                    .AddText(message.Text)
                    .AddMessage();
            }

            builder.EnableStreaming(true);
            ApplyOptionsToBuilder(builder, options);
            return builder.Build();
        }

        private static void ApplyOptionsToBuilder(ContentRequestBuilder builder, GeminiRequestOptions? options)
        {
            if (options == null) return;

            if (!string.IsNullOrEmpty(options.Model))
            {
                ValidationUtilities.ValidateModelName(options.Model);
            }

            if (options.Temperature.HasValue)
                builder.WithTemperature(options.Temperature.Value);

            if (options.TopP.HasValue)
                builder.WithTopP(options.TopP.Value);

            if (options.TopK.HasValue)
                builder.WithTopK(options.TopK.Value);

            if (options.MaxTokens.HasValue)
                builder.WithMaxOutputTokens(options.MaxTokens.Value);

            if (options.StopSequences?.Count > 0)
                builder.WithStopSequences(options.StopSequences.ToArray());

            if (options.SafetySettings?.Count > 0)
                builder.WithSafetySettings(options.SafetySettings);
        }

        private string GetGenerateEndpoint(string? model) =>
            $"models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:generateContent";

        private string GetStreamEndpoint(string? model) =>
            $"models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:streamGenerateContent";

        private string GetCountTokensEndpoint(string? model) =>
            $"models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:countTokens";

        #endregion Private Helper Methods
    }
}