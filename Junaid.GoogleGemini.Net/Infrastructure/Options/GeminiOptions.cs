using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using System.ComponentModel.DataAnnotations;

namespace Junaid.GoogleGemini.Net.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for the Gemini API client
    /// </summary>
    public class GeminiOptions
    {
        /// <summary>
        /// Base URL for the Gemini API
        /// </summary>
        [Required]
        public Uri BaseUrl { get; set; } = new Uri(GeminiConstants.DefaultBaseUrl);

        /// <summary>
        /// API key for authentication
        /// Can be set via configuration or environment variable 'GeminiApiKey'
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = GetApiKeyFromEnvironment();

        /// <summary>
        /// Default timeout for API requests in seconds
        /// </summary>
        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum number of retries for transient failures (HTTP 429, 5xx, network errors).
        /// </summary>
        [Range(0, 5)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay for the exponential backoff between retries. Lower it (e.g. to near-zero) in
        /// tests to keep them fast.
        /// </summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Default model to use for requests
        /// </summary>
        public string DefaultModel { get; set; } = GeminiConstants.Defaults.Model;

        /// <summary>
        /// Rate limiting settings
        /// </summary>
        public RateLimitOptions RateLimit { get; set; } = new();

        /// <summary>
        /// Proxy settings
        /// </summary>
        public ProxyOptions? Proxy { get; set; }

        /// <summary>
        /// Gets API key from environment variable if not set in configuration
        /// </summary>
        private static string GetApiKeyFromEnvironment()
        {
            return Environment.GetEnvironmentVariable("GeminiApiKey") ?? string.Empty;
        }
    }

    /// <summary>
    /// Rate limiting configuration options
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Maximum number of requests per minute
        /// </summary>
        [Range(1, 1000)]
        public int RequestsPerMinute { get; set; } = 60;

        /// <summary>
        /// Maximum number of tokens per minute
        /// </summary>
        [Range(1, 1000000)]
        public int TokensPerMinute { get; set; } = 60000;

        /// <summary>
        /// Whether to enable rate limiting
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Proxy configuration options
    /// </summary>
    public class ProxyOptions
    {
        /// <summary>
        /// Proxy server address
        /// </summary>
        [Required]
        public Uri Address { get; set; } = null!;

        /// <summary>
        /// Username for proxy authentication
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Password for proxy authentication
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Whether to bypass proxy for local addresses
        /// </summary>
        public bool BypassOnLocal { get; set; } = true;
    }
}