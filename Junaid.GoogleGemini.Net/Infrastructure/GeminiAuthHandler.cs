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
                if (!request.Headers.Contains(AUTH_HEADER))
                {
                    request.Headers.TryAddWithoutValidation(AUTH_HEADER, apiKey);
                }

                // Common headers (the client already sets the X-Correlation-ID we log below).
                if (request.Headers.Accept.Count == 0)
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
                request.Headers.UserAgent.TryParseAdd("Junaid.GoogleGemini.Net");

                var response = await base.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var correlationId = request.Headers.TryGetValues("X-Correlation-ID", out var ids)
                        ? string.Join(",", ids)
                        : "n/a";
                    _logger.LogWarning(
                        "Request failed - Status: {StatusCode} [ID: {CorrelationId}]",
                        response.StatusCode,
                        correlationId);
                }

                return response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
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