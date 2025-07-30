using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

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
        /// Validates API key format
        /// </summary>
        /// <param name="apiKey">API key to validate</param>
        /// <returns>True if valid format</returns>
        public static bool IsValidApiKeyFormat(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            // Basic validation - Google API keys typically start with specific prefixes
            return apiKey.Length >= 20 && 
                   (apiKey.StartsWith("AIza", StringComparison.Ordinal) || 
                    apiKey.StartsWith("BIza", StringComparison.Ordinal) ||
                    apiKey.StartsWith("CIza", StringComparison.Ordinal));
        }

        #endregion

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

        #endregion

        #region Legacy Migration Support

        /// <summary>
        /// Converts legacy GenerateContentConfiguration to modern GeminiRequestOptions
        /// </summary>
        /// <param name="legacyConfig">Legacy request configuration</param>
        /// <returns>Modern request options</returns>
        [Obsolete("This method helps migrate from legacy configuration. Use GeminiRequestOptions directly instead.")]
        public static GeminiRequestOptions ToGeminiRequestOptions(this GenerateContentConfiguration legacyConfig)
        {
            return new GeminiRequestOptions
            {
                Temperature = legacyConfig.generationConfig?.Temperature,
                MaxTokens = legacyConfig.generationConfig?.MaxOutputTokens,
                TopP = legacyConfig.generationConfig?.TopP,
                TopK = legacyConfig.generationConfig?.TopK,
                StopSequences = legacyConfig.generationConfig?.StopSequences,
                SafetySettings = legacyConfig.safetySettings?.ToList()
            };
        }

        /// <summary>
        /// Gets comprehensive migration examples and guidance
        /// </summary>
        /// <returns>Migration guide text</returns>
        public static string GetMigrationGuide()
        {
            return @"
# Configuration Migration Guide

## Service Registration
? OLD (removed in v7.0.0):
services.AddGeminiServices(config => 
{
    config.ApiKey = ""your-api-key"";
});

? NEW (recommended):
services.AddGemini(options => 
{
    options.ApiKey = ""your-api-key"";
    options.DefaultModel = GeminiConstants.Models.Recommended;
    options.TimeoutSeconds = 30;
});

## Request Configuration
? OLD (deprecated):
var legacyConfig = new GenerateContentConfiguration
{
    generationConfig = new GenerationConfig { Temperature = 0.8f },
    safetySettings = new[] { /* settings */ }
};

? NEW (recommended):
var options = new GeminiRequestOptions
{
    Temperature = 0.8f,
    SafetySettings = new List<SafetySetting> { /* settings */ }
};

## Using Utilities
?? Default configuration:
var options = ConfigurationUtilities.CreateDefaultOptions();

?? Production configuration:
var options = ConfigurationUtilities.CreateProductionOptions(""your-api-key"");

??? Development configuration:
var options = ConfigurationUtilities.CreateDevelopmentOptions(""your-api-key"");
";
        }

        #endregion

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

        #endregion
    }
}