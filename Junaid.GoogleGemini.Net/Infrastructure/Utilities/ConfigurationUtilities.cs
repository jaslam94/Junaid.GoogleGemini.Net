using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Infrastructure.Utilities
{
    /// <summary>
    /// Utility class for configuration management
    /// </summary>
    public static class ConfigurationUtilities
    {
        #region Environment Variable Helpers

        /// <summary>
        /// Gets API key from environment variables with fallback options
        /// </summary>
        /// <param name="primaryVariable">Primary environment variable name (default: GeminiApiKey)</param>
        /// <param name="fallbackVariables">Fallback environment variable names</param>
        /// <returns>API key if found, otherwise empty string</returns>
        public static string GetApiKeyFromEnvironment(
            string primaryVariable = GeminiConstants.ApiKeyEnvironmentVariable,
            params string[] fallbackVariables)
        {
            var apiKey = Environment.GetEnvironmentVariable(primaryVariable);
            if (!string.IsNullOrWhiteSpace(apiKey))
                return apiKey;

            foreach (var variable in fallbackVariables)
            {
                apiKey = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    return apiKey;
            }

            return string.Empty;
        }

        /// <summary>
        /// Sanity-checks an API key's shape, not its validity. Only the real API call can confirm
        /// that. Deliberately <b>not</b> locked to a specific prefix like <c>"AIza"</c>: Google has
        /// already changed the Gemini API key format once (legacy "AIza..." keys became the newer
        /// "AQ...." format, staged rollout through September 2026, after which "AIza" keys stop working
        /// entirely) and may again. A prefix allow-list here would silently reject valid keys the
        /// moment the format changes. That is exactly what happened to early testers of the new format
        /// against earlier versions of this check. This only catches the obvious paste-error class of
        /// mistakes (empty, truncated, or containing whitespace from a bad copy/paste).
        /// </summary>
        /// <param name="apiKey">API key to validate</param>
        /// <returns>True if the key's shape looks plausible</returns>
        public static bool IsValidApiKeyFormat(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            return apiKey.Length >= 10 && !apiKey.Any(char.IsWhiteSpace);
        }

        #endregion Environment Variable Helpers

        #region Default Configuration Generators

        /// <summary>
        /// Creates default GeminiOptions with sensible defaults
        /// </summary>
        /// <param name="apiKey">API key (if null, attempts to load from environment)</param>
        /// <returns>Configured GeminiOptions</returns>
        public static GeminiOptions CreateDefaultOptions(string? apiKey = null)
        {
            return new GeminiOptions
            {
                ApiKey = apiKey ?? GetApiKeyFromEnvironment(),
                BaseUrl = new Uri(GeminiConstants.DefaultBaseUrl),
                TimeoutSeconds = GeminiConstants.Defaults.TimeoutSeconds,
                MaxRetries = GeminiConstants.Defaults.MaxRetries,
                DefaultModel = GeminiConstants.Defaults.Model,
                RateLimit = new RateLimitOptions
                {
                    Enabled = true,
                    RequestsPerMinute = GeminiConstants.Limits.DefaultRequestsPerMinute,
                    TokensPerMinute = GeminiConstants.Limits.DefaultTokensPerMinute
                }
            };
        }

        /// <summary>
        /// Creates GeminiOptions optimized for development/testing
        /// </summary>
        /// <param name="apiKey">API key</param>
        /// <returns>Development-optimized options</returns>
        public static GeminiOptions CreateDevelopmentOptions(string apiKey)
        {
            var options = CreateDefaultOptions(apiKey);
            options.TimeoutSeconds = 60; // Longer timeout for debugging
            options.MaxRetries = 1; // Fewer retries for faster feedback
            options.RateLimit.RequestsPerMinute = 30; // Lower rate limit
            return options;
        }

        /// <summary>
        /// Creates GeminiOptions optimized for production
        /// </summary>
        /// <param name="apiKey">API key</param>
        /// <returns>Production-optimized options</returns>
        public static GeminiOptions CreateProductionOptions(string apiKey)
        {
            var options = CreateDefaultOptions(apiKey);
            options.TimeoutSeconds = 30;
            options.MaxRetries = 5; // More retries for resilience
            options.RateLimit.RequestsPerMinute = 100; // Higher rate limit
            options.RateLimit.TokensPerMinute = 100000;
            return options;
        }

        #endregion Default Configuration Generators

        #region Safety Configuration Helpers

        /// <summary>
        /// Creates default safety settings dictionary
        /// </summary>
        /// <returns>Dictionary with default safety thresholds</returns>
        public static Dictionary<string, string> GetDefaultSafetyThresholds()
        {
            return new Dictionary<string, string>
            {
                { GeminiConstants.SafetyCategories.Harassment, GeminiConstants.SafetyThresholds.Medium },
                { GeminiConstants.SafetyCategories.HateSpeech, GeminiConstants.SafetyThresholds.Medium },
                { GeminiConstants.SafetyCategories.SexuallyExplicit, GeminiConstants.SafetyThresholds.High },
                { GeminiConstants.SafetyCategories.DangerousContent, GeminiConstants.SafetyThresholds.Medium }
            };
        }

        /// <summary>
        /// Creates safety settings list from thresholds dictionary
        /// </summary>
        /// <param name="thresholds">Safety thresholds dictionary</param>
        /// <returns>List of SafetySetting objects</returns>
        public static List<SafetySetting> CreateSafetySettings(Dictionary<string, string> thresholds)
        {
            return thresholds.Select(kvp => new SafetySetting
            {
                Category = kvp.Key,
                Threshold = kvp.Value
            }).ToList();
        }

        /// <summary>
        /// Creates strict safety settings for sensitive content
        /// </summary>
        /// <returns>List of strict safety settings</returns>
        public static List<SafetySetting> CreateStrictSafetySettings()
        {
            return GeminiConstants.SafetyCategories.All.Select(category => new SafetySetting
            {
                Category = category,
                Threshold = GeminiConstants.SafetyThresholds.Low
            }).ToList();
        }

        /// <summary>
        /// Creates permissive safety settings for general content
        /// </summary>
        /// <returns>List of permissive safety settings</returns>
        public static List<SafetySetting> CreatePermissiveSafetySettings()
        {
            return GeminiConstants.SafetyCategories.All.Select(category => new SafetySetting
            {
                Category = category,
                Threshold = GeminiConstants.SafetyThresholds.High
            }).ToList();
        }

        #endregion Safety Configuration Helpers
    }
}