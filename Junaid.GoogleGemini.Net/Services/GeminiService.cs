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
        private const int MAX_TEXT_LENGTH = 100000;
        private const int MAX_IMAGE_SIZE = 20 * 1024 * 1024; // 20MB
        private const int MAX_MESSAGES = 50;
        private const int MAX_MESSAGE_LENGTH = 10000;

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
            ValidateTextInput(prompt, nameof(prompt), MAX_TEXT_LENGTH);
            ValidateImageFile(image);
            ValidateStreamHandler(handleResponse);

            var request = CreateVisionRequest(prompt, image, options, streaming: true);
            var endpoint = GetStreamEndpoint(options?.Model ?? "gemini-1.5-pro");

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
            ValidateMessages(messages);
            ValidateStreamHandler(handleResponse);

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
            ValidateTextInput(prompt, nameof(prompt), MAX_TEXT_LENGTH);

            var request = CreateTextRequest(prompt, null);
            var endpoint = GetCountTokensEndpoint(null);

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
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
            ValidateTextInput(prompt, nameof(prompt), MAX_TEXT_LENGTH);
            ValidateImageFile(image);

            var request = CreateVisionRequest(prompt, image, null);
            var endpoint = GetCountTokensEndpoint("gemini-1.5-pro");

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
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
            ValidateMessages(messages);

            var request = CreateChatRequest(messages, null);
            var endpoint = GetCountTokensEndpoint(null);

            return await ExecuteRequestAsync<GenerateContentRequest, CountTokensResponse>(
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
            $"/models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:generateContent";

        private string GetStreamEndpoint(string? model) =>
            $"/models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:streamGenerateContent";

        private string GetCountTokensEndpoint(string? model) =>
            $"/models/{model ?? Options?.DefaultModel ?? GeminiConstants.Defaults.Model}:countTokens";

        private static void ValidateImageFile(FileObject image)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (string.IsNullOrWhiteSpace(image.FileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(image));

            if (image.FileContent == null || image.FileContent.Length == 0)
                throw new ArgumentException("File content cannot be null or empty", nameof(image));

            if (image.FileContent.Length > MAX_IMAGE_SIZE)
                throw new ArgumentException($"Image size exceeds maximum limit of {MAX_IMAGE_SIZE / (1024 * 1024)}MB", nameof(image));

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!SupportedImageTypes.Contains(extension))
                throw new ArgumentException($"Unsupported image type: {extension}. Supported types: {string.Join(", ", SupportedImageTypes)}", nameof(image));
        }

        private static void ValidateMessages(MessageObject[] messages)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));

            if (messages.Length == 0)
                throw new ArgumentException("Messages array cannot be empty", nameof(messages));

            if (messages.Length > MAX_MESSAGES)
                throw new ArgumentException($"Too many messages. Maximum allowed: {MAX_MESSAGES}", nameof(messages));

            var validRoles = new[] { "user", "model" };
            for (int i = 0; i < messages.Length; i++)
            {
                var message = messages[i];
                if (message == null)
                    throw new ArgumentException($"Message at index {i} cannot be null", nameof(messages));

                if (string.IsNullOrWhiteSpace(message.Role))
                    throw new ArgumentException($"Message role at index {i} cannot be null or empty", nameof(messages));

                if (!validRoles.Contains(message.Role.ToLowerInvariant()))
                    throw new ArgumentException($"Invalid message role '{message.Role}' at index {i}. Must be 'user' or 'model'", nameof(messages));

                if (string.IsNullOrWhiteSpace(message.Text))
                    throw new ArgumentException($"Message text at index {i} cannot be null or empty", nameof(messages));

                if (message.Text.Length > MAX_MESSAGE_LENGTH)
                    throw new ArgumentException($"Message at index {i} exceeds maximum length of {MAX_MESSAGE_LENGTH:N0} characters", nameof(messages));
            }
        }

        #endregion Private Helper Methods
    }
}