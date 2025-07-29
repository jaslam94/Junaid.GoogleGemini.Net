using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Constants;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
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
        /// Gets the default safety thresholds used across all services
        /// </summary>
        protected static Dictionary<string, string> GetDefaultSafetyThresholds()
        {
            return new Dictionary<string, string>
            {
                { SafetyCategory.Harassment, SafetyThreshold.Medium },
                { SafetyCategory.HateSpeech, SafetyThreshold.Medium },
                { SafetyCategory.SexuallyExplicit, SafetyThreshold.High },
                { SafetyCategory.DangerousContent, SafetyThreshold.Medium }
            };
        }

        /// <summary>
        /// Validates a response and checks safety requirements
        /// </summary>
        protected void ValidateResponse(GenerateContentResponse response, string operation = "operation")
        {
            if (response?.candidates == null || response.candidates.Length == 0)
            {
                throw new InvalidOperationException($"No content was generated for {operation}");
            }

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
            if (handleStreamResponse == null)
            {
                throw new ArgumentNullException(nameof(handleStreamResponse), "Stream response handler cannot be null");
            }
        }

        /// <summary>
        /// Validates basic text input with optional length limits
        /// </summary>
        protected static void ValidateTextInput(string text, string paramName = "text", int maxLength = 100000)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
            }

            if (text.Length > maxLength)
            {
                throw new ArgumentException($"{paramName} exceeds maximum length of {maxLength:N0} characters", paramName);
            }
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