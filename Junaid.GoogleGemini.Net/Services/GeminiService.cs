using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

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
        public async IAsyncEnumerable<GenerateContentResponse> StreamAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);

            var request = CreateTextRequest(prompt, options);
            var endpoint = GetStreamEndpoint(options?.Model);

            await foreach (var chunk in StreamRequestAsync(endpoint, request, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<GenerateContentResponse> StreamWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(image, nameof(image));

            var request = CreateVisionRequest(prompt, image, options);
            var endpoint = GetStreamEndpoint(options?.Model);

            await foreach (var chunk in StreamRequestAsync(endpoint, request, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<GenerateContentResponse> StreamChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));

            var request = CreateChatRequest(messages, options);
            var endpoint = GetStreamEndpoint(options?.Model);

            await foreach (var chunk in StreamRequestAsync(endpoint, request, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <inheritdoc/>
        public async Task StreamAsync(
            string prompt,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            await foreach (var chunk in StreamAsync(prompt, options, cancellationToken))
            {
                handleResponse(chunk.Text());
            }
        }

        /// <inheritdoc/>
        public async Task StreamWithImageAsync(
            string prompt,
            FileObject image,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            await foreach (var chunk in StreamWithImageAsync(prompt, image, options, cancellationToken))
            {
                handleResponse(chunk.Text());
            }
        }

        /// <inheritdoc/>
        public async Task StreamChatAsync(
            MessageObject[] messages,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateStreamHandler(handleResponse, nameof(handleResponse));

            await foreach (var chunk in StreamChatAsync(messages, options, cancellationToken))
            {
                handleResponse(chunk.Text());
            }
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

        // Streaming and non-streaming requests have identical bodies (streaming is purely an endpoint
        // concern), so both paths build the request the same way via the factory.
        private static GenerateContentRequest CreateTextRequest(string prompt, GeminiRequestOptions? options) =>
            RequestFactory.CreateTextRequest(prompt, options);

        private static GenerateContentRequest CreateVisionRequest(string prompt, FileObject image, GeminiRequestOptions? options) =>
            RequestFactory.CreateVisionRequest(prompt, image, options);

        private static GenerateContentRequest CreateChatRequest(MessageObject[] messages, GeminiRequestOptions? options) =>
            RequestFactory.CreateChatRequest(messages, options);

        private string GetGenerateEndpoint(string? model) =>
            $"models/{FormatModelName(model, Options?.DefaultModel)}:generateContent";

        // ?alt=sse makes the API emit Server-Sent Events ("data: {json}" lines), which the client parses.
        private string GetStreamEndpoint(string? model) =>
            $"models/{FormatModelName(model, Options?.DefaultModel)}:streamGenerateContent?alt=sse";

        private string GetCountTokensEndpoint(string? model) =>
            $"models/{FormatModelName(model, Options?.DefaultModel)}:countTokens";

        private static string FormatModelName(string? model, string? defaultModel)
        {
            var modelName = model ?? defaultModel ?? GeminiConstants.Defaults.Model;
            ValidationUtilities.ValidateModelName(modelName);
            return modelName;
        }

        #endregion Private Helper Methods
    }
}