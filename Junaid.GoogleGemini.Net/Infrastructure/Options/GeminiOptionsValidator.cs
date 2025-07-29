using Microsoft.Extensions.Options;

namespace Junaid.GoogleGemini.Net.Infrastructure.Options
{
    /// <summary>
    /// Validates GeminiOptions configuration at startup
    /// </summary>
    public class GeminiOptionsValidator : IValidateOptions<GeminiOptions>
    {
        private const int MIN_TIMEOUT_SECONDS = 1;
        private const int MAX_TIMEOUT_SECONDS = 300;
        private const int MAX_RETRIES = 5;
        private const int MIN_REQUESTS_PER_MINUTE = 1;
        private const int MAX_REQUESTS_PER_MINUTE = 1000;
        private const int MIN_TOKENS_PER_MINUTE = 1;
        private const int MAX_TOKENS_PER_MINUTE = 1000000;

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
                var envApiKey = Environment.GetEnvironmentVariable("GeminiApiKey");
                if (string.IsNullOrWhiteSpace(envApiKey))
                {
                    errors.Add("API key must be provided either in configuration or as 'GeminiApiKey' environment variable.");
                }
                else
                {
                    options.ApiKey = envApiKey;
                }
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
            if (options.TimeoutSeconds < MIN_TIMEOUT_SECONDS || options.TimeoutSeconds > MAX_TIMEOUT_SECONDS)
            {
                errors.Add($"Timeout must be between {MIN_TIMEOUT_SECONDS} and {MAX_TIMEOUT_SECONDS} seconds.");
            }
        }

        private static void ValidateRetries(GeminiOptions options, List<string> errors)
        {
            if (options.MaxRetries < 0 || options.MaxRetries > MAX_RETRIES)
            {
                errors.Add($"MaxRetries must be between 0 and {MAX_RETRIES}.");
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
                if (options.RateLimit.RequestsPerMinute < MIN_REQUESTS_PER_MINUTE || 
                    options.RateLimit.RequestsPerMinute > MAX_REQUESTS_PER_MINUTE)
                {
                    errors.Add($"RequestsPerMinute must be between {MIN_REQUESTS_PER_MINUTE} and {MAX_REQUESTS_PER_MINUTE}.");
                }

                if (options.RateLimit.TokensPerMinute < MIN_TOKENS_PER_MINUTE || 
                    options.RateLimit.TokensPerMinute > MAX_TOKENS_PER_MINUTE)
                {
                    errors.Add($"TokensPerMinute must be between {MIN_TOKENS_PER_MINUTE} and {MAX_TOKENS_PER_MINUTE}.");
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
                var validModels = new[] { "gemini-pro", "gemini-pro-vision", "embedding-001" };
                if (!validModels.Contains(options.DefaultModel))
                {
                    errors.Add($"Default model must be one of: {string.Join(", ", validModels)}");
                }
            }
        }
    }
}