using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Base service class with common functionality for all Gemini API services
    /// </summary>
    public abstract class Service
    {
        protected readonly IGeminiClient GeminiClient;
        protected readonly ILogger? Logger;
        protected readonly GeminiOptions? Options;
        protected readonly ISafetyService? SafetyService;

        protected Service(IGeminiClient geminiClient)
        {
            GeminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
        }

        protected Service(
            IGeminiClient geminiClient,
            ILogger logger,
            IOptions<GeminiOptions> options,
            ISafetyService? safetyService)
        {
            GeminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            SafetyService = safetyService; // Can be null for some services like EmbeddingService
        }

        /// <summary>
        /// Common method to execute API requests with consistent error handling and logging
        /// </summary>
        protected async Task<TResponse> ExecuteRequestAsync<TRequest, TResponse>(
            string operation,
            string endpoint,
            TRequest request,
            object? logContext = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await GeminiClient.PostAsync<TRequest, TResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                if (response is GenerateContentResponse contentResponse)
                {
                    ValidateResponse(contentResponse, operation);
                }

                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                Logger?.LogError(ex, "API {Operation} failed", operation);
                throw;
            }
        }

        /// <summary>
        /// Streams a request through the client, yielding each response chunk. Cancellation flows to
        /// the underlying HTTP read.
        /// </summary>
        protected async IAsyncEnumerable<GenerateContentResponse> StreamRequestAsync<TRequest>(
            string endpoint,
            TRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in GeminiClient.StreamAsync(endpoint, request, cancellationToken))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Gets the default safety thresholds used across all services
        /// </summary>
        protected static Dictionary<string, string> GetDefaultSafetyThresholds()
        {
            return ConfigurationUtilities.GetDefaultSafetyThresholds();
        }

        /// <summary>
        /// Validates a response and checks safety requirements
        /// </summary>
        protected void ValidateResponse(GenerateContentResponse response, string operation = "operation")
        {
            ValidationUtilities.ValidateContentResponse(response, operation);

            // Note: Safety validation removed from base class as it should be handled
            // per-request based on the actual safety settings sent with the request.
            // The API itself will block content that violates the requested safety settings.
        }

        /// <summary>
        /// Validates that a stream handler is not null
        /// </summary>
        protected static void ValidateStreamHandler(Action<string> handleStreamResponse)
        {
            ValidationUtilities.ValidateStreamHandler(handleStreamResponse);
        }

        /// <summary>
        /// Validates basic text input with optional length limits
        /// </summary>
        protected static void ValidateTextInput(string text, string paramName = "text", int maxLength = GeminiConstants.Limits.MaxTextLength)
        {
            ValidationUtilities.ValidateTextInput(text, paramName, maxLength);
        }

        /// <summary>
        /// Logs operation start only when necessary
        /// </summary>
        protected void LogOperationStart(string operation, object? context = null)
        {
            // Only log complex operations, not every single request
            Logger?.LogDebug("{Operation} started", operation);
        }

        /// <summary>
        /// Logs successful operation completion
        /// </summary>
        protected void LogOperationSuccess(string operation, object? result = null)
        {
            // Simplified success logging
            Logger?.LogDebug("{Operation} completed", operation);
        }

        /// <summary>
        /// Logs an operation error with response details
        /// </summary>
        protected void LogOperationError(Exception ex, string operation)
        {
            Logger?.LogError(ex, "Operation {Operation} failed", operation);
        }
    }
}