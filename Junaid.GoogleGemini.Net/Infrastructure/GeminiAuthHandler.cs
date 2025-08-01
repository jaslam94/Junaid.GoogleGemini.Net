using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>
    /// HTTP message handler that adds authentication headers to requests for the Gemini API
    /// </summary>
    public class GeminiAuthHandler : DelegatingHandler
    {
        private readonly ILogger<GeminiAuthHandler> _logger;
        private readonly GeminiOptions _options;
        private const string AUTH_HEADER = "x-goog-api-key";

        /// <summary>
        /// Initializes a new instance of the GeminiAuthHandler
        /// </summary>
        /// <param name="options">Configuration options</param>
        /// <param name="logger">Logger for diagnostic information</param>
        public GeminiAuthHandler(
            IOptions<GeminiOptions> options,
            ILogger<GeminiAuthHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Adds authentication headers to the request and forwards it to the inner handler
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                var apiKey = GetApiKey();
                request.Headers.Add(AUTH_HEADER, apiKey);

                // Add common headers
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("User-Agent", "Junaid.GoogleGemini.Net");

                // Add correlation ID for request tracing
                var correlationId = Guid.NewGuid().ToString();
                request.Headers.Add("X-Correlation-ID", correlationId);

                var response = await base.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Request failed - Status: {StatusCode} [ID: {CorrelationId}]",
                        response.StatusCode,
                        correlationId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing authentication");
                throw;
            }
        }

        /// <summary>
        /// Gets the API key from configuration or environment variables
        /// </summary>
        private string GetApiKey()
        {
            // Try configuration first
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return _options.ApiKey;
            }

            // Try environment variable
            var envApiKey = Environment.GetEnvironmentVariable("GeminiApiKey");
            if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                return envApiKey;
            }

            _logger.LogError("API key not found in configuration or environment variables");
            throw new InvalidOperationException(
                "API key is required. Set it in configuration or environment variable 'GeminiApiKey'. " +
                "You can get an API key from Google AI Studio: https://makersuite.google.com/app/apikey");
        }
    }
}