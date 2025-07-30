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
        public Uri BaseUrl { get; set; } = new Uri("https://generativelanguage.googleapis.com/v1/");

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
        /// Maximum number of retries for failed requests
        /// </summary>
        [Range(0, 5)]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Default model to use for requests
        /// </summary>
        public string DefaultModel { get; set; } = "gemini-1.5-pro";

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