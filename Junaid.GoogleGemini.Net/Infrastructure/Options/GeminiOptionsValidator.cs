using Microsoft.Extensions.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Options
{
    /// <summary>
    /// Validates GeminiOptions configuration at startup using unified constants
    /// </summary>
    public class GeminiOptionsValidator : IValidateOptions<GeminiOptions>
    {
        /// <inheritdoc/>
        public ValidateOptionsResult Validate(string? name, GeminiOptions options)
        {
            var errors = new List<string>();

            if (options == null)
            {
                return ValidateOptionsResult.Fail("GeminiOptions must be provided.");
            }

            ValidateApiKey(options, errors);
            ValidateBaseUrl(options, errors);
            ValidateTimeouts(options, errors);
            ValidateRetries(options, errors);
            ValidateRateLimits(options, errors);
            ValidateProxy(options, errors);
            ValidateModel(options, errors);

            if (errors.Count > 0)
            {
                return ValidateOptionsResult.Fail(errors);
            }

            return ValidateOptionsResult.Success;
        }

        private static void ValidateApiKey(GeminiOptions options, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                var envApiKey = ConfigurationUtilities.GetApiKeyFromEnvironment();
                if (string.IsNullOrWhiteSpace(envApiKey))
                {
                    errors.Add($"API key must be provided either in configuration or as '{GeminiConstants.ApiKeyEnvironmentVariable}' environment variable.");
                }
                else
                {
                    options.ApiKey = envApiKey;
                }
            }

            // Validate API key format if provided
            if (!string.IsNullOrWhiteSpace(options.ApiKey) && !ConfigurationUtilities.IsValidApiKeyFormat(options.ApiKey))
            {
                errors.Add("API key format appears to be invalid. Please verify your API key.");
            }
        }

        private static void ValidateBaseUrl(GeminiOptions options, List<string> errors)
        {
            if (options.BaseUrl == null)
            {
                errors.Add("Base URL must be provided.");
            }
            else if (!options.BaseUrl.IsAbsoluteUri)
            {
                errors.Add("Base URL must be an absolute URI.");
            }
            else if (!options.BaseUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Base URL must use HTTPS.");
            }
        }

        private static void ValidateTimeouts(GeminiOptions options, List<string> errors)
        {
            if (options.TimeoutSeconds < 1 || options.TimeoutSeconds > 300)
            {
                errors.Add("Timeout must be between 1 and 300 seconds.");
            }
        }

        private static void ValidateRetries(GeminiOptions options, List<string> errors)
        {
            if (options.MaxRetries < 0 || options.MaxRetries > GeminiConstants.Defaults.MaxRetries)
            {
                errors.Add($"MaxRetries must be between 0 and {GeminiConstants.Defaults.MaxRetries}.");
            }
        }

        private static void ValidateRateLimits(GeminiOptions options, List<string> errors)
        {
            if (options.RateLimit == null)
            {
                errors.Add("RateLimit settings must be provided.");
                return;
            }

            if (options.RateLimit.Enabled)
            {
                if (options.RateLimit.RequestsPerMinute < 1 || 
                    options.RateLimit.RequestsPerMinute > 1000)
                {
                    errors.Add("RequestsPerMinute must be between 1 and 1000.");
                }

                if (options.RateLimit.TokensPerMinute < 1 || 
                    options.RateLimit.TokensPerMinute > 1000000)
                {
                    errors.Add("TokensPerMinute must be between 1 and 1000000.");
                }
            }
        }

        private static void ValidateProxy(GeminiOptions options, List<string> errors)
        {
            if (options.Proxy != null)
            {
                if (options.Proxy.Address == null)
                {
                    errors.Add("Proxy address must be provided when proxy is configured.");
                }
                else if (!options.Proxy.Address.IsAbsoluteUri)
                {
                    errors.Add("Proxy address must be an absolute URI.");
                }

                if (!string.IsNullOrEmpty(options.Proxy.Username) && string.IsNullOrEmpty(options.Proxy.Password))
                {
                    errors.Add("Proxy password must be provided when username is set.");
                }
            }
        }

        private static void ValidateModel(GeminiOptions options, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(options.DefaultModel))
            {
                errors.Add("Default model must be specified.");
            }
            else
            {
                try
                {
                    ValidationUtilities.ValidateModelName(options.DefaultModel);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Default model validation failed: {ex.Message}");
                }
            }
        }
    }
}