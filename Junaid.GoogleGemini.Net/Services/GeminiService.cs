using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Unified service implementation for all Gemini content generation operations
    /// </summary>
    public class GeminiService(
        IGeminiClient geminiClient,
        ILogger<GeminiService> logger,
        IOptions<GeminiOptions> options,
        ISafetyService safetyService,
        ICostGovernor costGovernor) : Service(geminiClient, logger, options, safetyService), IGeminiService
    {
        /// <inheritdoc/>
        public Task<GenerateContentResponse> GenerateAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default) =>
            GenerateInternalAsync(prompt, options, "text generation", cancellationToken);

        // Shared by GenerateAsync and GenerateImageAsync: identical request-building, only the
        // operation label differs (it shows up in error logs and in the "No content was generated
        // for {operation}" exception). Mislabeling it as "text generation" for an image call would
        // be confusing when debugging a fully-blocked image prompt.
        private async Task<GenerateContentResponse> GenerateInternalAsync(
            string prompt,
            GeminiRequestOptions? options,
            string operation,
            CancellationToken cancellationToken)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            await CheckPerRequestBudgetForTextAsync(prompt, options, cancellationToken);

            var request = CreateTextRequest(prompt, options);
            var endpoint = GetGenerateEndpoint(options?.Model);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                operation,
                endpoint,
                request,
                new { PromptLength = prompt.Length, Model = options?.Model ?? Options?.DefaultModel },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<T> GenerateAsync<T>(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);

            // Constrain the request to JSON and derive a schema from T (unless the caller set them).
            var structuredOptions = options?.Clone() ?? new GeminiRequestOptions();
            structuredOptions.ResponseMimeType ??= "application/json";
            structuredOptions.ResponseSchema ??= JsonSchemaGenerator.Generate(typeof(T));

            var response = await GenerateAsync(prompt, structuredOptions, cancellationToken);
            var json = response.GetTextOrThrow();

            try
            {
                return JsonSerializer.Deserialize<T>(json, GeminiJson.Default)
                    ?? throw new GeminiSerializationException(
                        $"The structured response for {typeof(T).Name} deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new GeminiSerializationException(
                    $"Failed to deserialize the structured response into {typeof(T).Name}.", ex);
            }
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
            await CheckPerRequestBudgetForImageAsync(prompt, image, options, cancellationToken);

            var request = CreateVisionRequest(prompt, image, options);
            // All current Gemini models are multimodal, so vision uses the default model unless overridden.
            var endpoint = GetGenerateEndpoint(options?.Model);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "vision generation",
                endpoint,
                request,
                new { PromptLength = prompt.Length, ImageSize = image.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<GenerateContentResponse> GenerateImageAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            // A thin wrapper: fill in image-generation defaults the caller didn't set, then reuse the
            // same request-building/validation/retry path as GenerateAsync (via GenerateInternalAsync).
            // No logic is duplicated, only a distinct operation label for accurate logs and errors.
            var imageOptions = options?.Clone() ?? new GeminiRequestOptions();
            imageOptions.Model ??= GeminiConstants.Models.RecommendedImage;
            imageOptions.ResponseModalities ??=
            [
                GeminiConstants.ResponseModalities.Text,
                GeminiConstants.ResponseModalities.Image
            ];

            return GenerateInternalAsync(prompt, imageOptions, "image generation", cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<GenerateContentResponse> ChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));
            await CheckPerRequestBudgetForChatAsync(messages, options, cancellationToken);

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
        public async Task<GenerateContentResponse> ChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (contents is null || contents.Count == 0)
            {
                throw new ArgumentException("At least one content item is required.", nameof(contents));
            }
            await CheckPerRequestBudgetForContentAsync(contents, options, cancellationToken);

            var request = RequestFactory.CreateRequest(contents, options);
            var endpoint = GetGenerateEndpoint(options?.Model);

            return await ExecuteRequestAsync<GenerateContentRequest, GenerateContentResponse>(
                "chat generation",
                endpoint,
                request,
                new { MessageCount = contents.Count },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<GenerateContentResponse> StreamChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (contents is null || contents.Count == 0)
            {
                throw new ArgumentException("At least one content item is required.", nameof(contents));
            }
            await CheckPerRequestBudgetForContentAsync(contents, options, cancellationToken);

            var request = RequestFactory.CreateRequest(contents, options);
            var endpoint = GetStreamEndpoint(options?.Model);

            await foreach (var chunk in StreamRequestAsync(endpoint, request, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<GenerateContentResponse> StreamAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            await CheckPerRequestBudgetForTextAsync(prompt, options, cancellationToken);

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
            await CheckPerRequestBudgetForImageAsync(prompt, image, options, cancellationToken);

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
            await CheckPerRequestBudgetForChatAsync(messages, options, cancellationToken);

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
        public Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            CancellationToken cancellationToken = default) =>
            CountTokensAsync(prompt, options: null, cancellationToken);

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);

            var request = RequestFactory.CreateTokenCountingTextRequest(prompt);
            var endpoint = GetCountTokensEndpoint(options?.Model);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "token counting",
                endpoint,
                request,
                new { PromptLength = prompt.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            CancellationToken cancellationToken = default) =>
            CountTokensWithImageAsync(prompt, image, options: null, cancellationToken);

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateTextInput(prompt, nameof(prompt), GeminiConstants.Limits.MaxTextLength);
            ValidationUtilities.ValidateFileObject(image, nameof(image));

            var request = RequestFactory.CreateTokenCountingVisionRequest(prompt, image);
            var endpoint = GetCountTokensEndpoint(options?.Model);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "vision token counting",
                endpoint,
                request,
                new { PromptLength = prompt.Length, ImageSize = image.FileContent.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            CancellationToken cancellationToken = default) =>
            CountTokensChatAsync(messages, options: null, cancellationToken);

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateMessages(messages, nameof(messages));

            var request = RequestFactory.CreateTokenCountingChatRequest(messages);
            var endpoint = GetCountTokensEndpoint(options?.Model);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "chat token counting",
                endpoint,
                request,
                new { MessageCount = messages.Length },
                cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<CountTokensResponse> CountTokensChatAsync(
            IList<Content> contents,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (contents is null || contents.Count == 0)
            {
                throw new ArgumentException("At least one content item is required.", nameof(contents));
            }

            var request = RequestFactory.CreateTokenCountingRequest(contents);
            var endpoint = GetCountTokensEndpoint(options?.Model);

            return await ExecuteRequestAsync<CountTokensRequest, CountTokensResponse>(
                "chat token counting",
                endpoint,
                request,
                new { MessageCount = contents.Count },
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

        #region Per-Request Budget Estimate (BudgetOptions.MaxCostPerRequestUsd)

        // All three overloads below share the same shape: bail out with zero overhead when no ceiling
        // is configured (ICostGovernor.HasRequestCeiling is a cheap in-memory check), otherwise spend
        // one real CountTokensAsync round-trip to get an exact input-token count, then hand it to the
        // governor together with a best-effort output-token bound. See the XML docs on
        // BudgetOptions.MaxCostPerRequestUsd for exactly what this can and cannot guarantee.

        private async Task CheckPerRequestBudgetForTextAsync(string prompt, GeminiRequestOptions? options, CancellationToken cancellationToken)
        {
            if (!costGovernor.HasRequestCeiling)
            {
                return;
            }

            var counted = await CountTokensAsync(prompt, options, cancellationToken);
            costGovernor.CheckEstimatedRequestCost(options?.Model ?? Options?.DefaultModel, counted.TotalTokens, ResolveMaxOutputTokens(options));
        }

        private async Task CheckPerRequestBudgetForImageAsync(string prompt, FileObject image, GeminiRequestOptions? options, CancellationToken cancellationToken)
        {
            if (!costGovernor.HasRequestCeiling)
            {
                return;
            }

            var counted = await CountTokensWithImageAsync(prompt, image, options, cancellationToken);
            costGovernor.CheckEstimatedRequestCost(options?.Model ?? Options?.DefaultModel, counted.TotalTokens, ResolveMaxOutputTokens(options));
        }

        private async Task CheckPerRequestBudgetForChatAsync(MessageObject[] messages, GeminiRequestOptions? options, CancellationToken cancellationToken)
        {
            if (!costGovernor.HasRequestCeiling)
            {
                return;
            }

            var counted = await CountTokensChatAsync(messages, options, cancellationToken);
            costGovernor.CheckEstimatedRequestCost(options?.Model ?? Options?.DefaultModel, counted.TotalTokens, ResolveMaxOutputTokens(options));
        }

        // Closes the gap called out in the ChatAsync(IList<Content>) doc history: there IS now a
        // countTokens counterpart for a raw Content list (RequestFactory.CreateTokenCountingRequest /
        // CountTokensChatAsync(IList<Content>, ...)), so the raw multi-turn overload gets the same
        // pre-flight estimate coverage as the MessageObject[]-based one, with the same general
        // limitations (standard-rate-only input, output bounded only when MaxTokens is set).
        private async Task CheckPerRequestBudgetForContentAsync(IList<Content> contents, GeminiRequestOptions? options, CancellationToken cancellationToken)
        {
            if (!costGovernor.HasRequestCeiling)
            {
                return;
            }

            var counted = await CountTokensChatAsync(contents, options, cancellationToken);
            costGovernor.CheckEstimatedRequestCost(options?.Model ?? Options?.DefaultModel, counted.TotalTokens, ResolveMaxOutputTokens(options));
        }

        // MaxTokens bounds candidate/output tokens; a positive ThinkingBudget bounds "thinking" tokens
        // separately -- but those bill as OUTPUT too (see GeminiCostGovernor.ComputeCost), so a request
        // that sets both needs the two added together to avoid understating the output-side bound.
        // ThinkingBudget of 0 (disabled) or -1 (model decides, unbounded) contributes nothing extra:
        // 0 adds no tokens, and -1 can't be turned into a token count at all, so it's excluded rather
        // than silently misread as "unlimited plus MaxTokens" or "minus one token".
        private static int? ResolveMaxOutputTokens(GeminiRequestOptions? options)
        {
            var maxTokens = options?.MaxTokens;
            if (maxTokens is not null && options?.ThinkingBudget is > 0)
            {
                maxTokens += options.ThinkingBudget;
            }
            return maxTokens;
        }

        #endregion Per-Request Budget Estimate (BudgetOptions.MaxCostPerRequestUsd)
    }
}