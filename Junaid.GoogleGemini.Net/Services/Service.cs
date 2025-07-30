using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
                LogOperationStart(operation, logContext);

                var response = await GeminiClient.PostAsync<TRequest, TResponse>(
                    endpoint,
                    request,
                    cancellationToken);

                if (response is GenerateContentResponse contentResponse)
                {
                    ValidateResponse(contentResponse, operation);
                }

                LogOperationSuccess(operation);
                return response;
            }
            catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
            {
                LogOperationError(ex, operation);
                throw;
            }
        }

        /// <summary>
        /// Common method to execute streaming requests with consistent error handling and logging
        /// </summary>
        protected async Task ExecuteStreamRequestAsync<TRequest>(
            string operation,
            string endpoint,
            TRequest request,
            Action<string> handleStreamResponse,
            object? logContext = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                LogOperationStart(operation, logContext);

                await foreach (var data in GeminiClient.SendAsync(endpoint, request).WithCancellation(cancellationToken))
                {
                    handleStreamResponse(data);
                }

                LogOperationSuccess(operation);
            }
            catch (Exception ex)
            {
                LogOperationError(ex, operation);
                throw;
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

            if (SafetyService != null && !SafetyService.IsContentSafe(response, GetDefaultSafetyThresholds()))
            {
                Logger?.LogWarning("Generated content for {Operation} failed safety checks", operation);
                throw new InvalidOperationException("Generated content failed safety checks");
            }
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
        /// Logs the start of an operation
        /// </summary>
        protected void LogOperationStart(string operation, object? context = null)
        {
            if (context != null)
            {
                Logger?.LogInformation("Starting {Operation} with context: {@Context}", operation, context);
            }
            else
            {
                Logger?.LogInformation("Starting {Operation}", operation);
            }
        }

        /// <summary>
        /// Logs the successful completion of an operation
        /// </summary>
        protected void LogOperationSuccess(string operation, object? result = null)
        {
            if (result != null)
            {
                Logger?.LogInformation("{Operation} completed successfully: {@Result}", operation, result);
            }
            else
            {
                Logger?.LogInformation("{Operation} completed successfully", operation);
            }
        }

        /// <summary>
        /// Logs an operation error
        /// </summary>
        protected void LogOperationError(Exception ex, string operation)
        {
            Logger?.LogError(ex, "Error during {Operation}", operation);
        }
    }
}