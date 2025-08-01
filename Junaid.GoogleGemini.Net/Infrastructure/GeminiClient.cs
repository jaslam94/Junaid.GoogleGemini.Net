using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>
    /// Client for interacting with the Google Gemini API
    /// </summary>
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly IRateLimiter _rateLimiter;

        /// <summary>
        /// Initializes a new instance of the GeminiClient
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for API requests</param>
        /// <param name="logger">Logger for diagnostic information</param>
        /// <param name="rateLimiter">Rate limiter for API requests</param>
        public GeminiClient(HttpClient httpClient, ILogger<GeminiClient> logger, IRateLimiter rateLimiter)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            
            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(response =>
                {
                    var statusCode = (int)response.StatusCode;
                    return statusCode == 429 || // Too Many Requests
                           statusCode >= 500;   // Server Errors
                })
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // exponential backoff
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        if (exception.Result != null)
                        {
                            _logger.LogWarning(
                                "Retry {RetryCount} after {DelayMs}ms - Status: {StatusCode}",
                                retryCount,
                                timeSpan.TotalMilliseconds,
                                exception.Result.StatusCode);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Retry {RetryCount} after {DelayMs}ms - Error: {Error}",
                                retryCount,
                                timeSpan.TotalMilliseconds,
                                exception.Exception?.Message);
                        }
                    });
        }

        /// <summary>
        /// Sends a GET request to the specified endpoint
        /// </summary>
        /// <typeparam name="TResponse">The type of the expected response</typeparam>
        /// <param name="endpoint">The API endpoint to send the request to</param>
        /// <returns>The deserialized response</returns>
        /// <exception cref="GeminiException">Thrown when the request fails or returns invalid data</exception>
        public async Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                // Apply rate limiting
                using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
                if (!lease.IsAcquired)
                {
                    throw new GeminiException("Rate limit exceeded. Please try again later.");
                }

                var response = await _retryPolicy.ExecuteAsync(async (ctx) =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                    request.Headers.Add("X-Correlation-ID", correlationId);
                    return await _httpClient.SendAsync(request, cancellationToken);
                }, new Context());

                return await HandleResponse<TResponse>(response, correlationId, cancellationToken)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "GET request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
                throw new GeminiException("Failed to make GET request to Gemini API", ex);
            }
        }

        /// <summary>
        /// Sends a POST request to the specified endpoint
        /// </summary>
        /// <typeparam name="TRequest">The type of the request data</typeparam>
        /// <typeparam name="TResponse">The type of the expected response</typeparam>
        /// <param name="endpoint">The API endpoint to send the request to</param>
        /// <param name="data">The request data to send</param>
        /// <returns>The deserialized response</returns>
        /// <exception cref="GeminiException">Thrown when the request fails or returns invalid data</exception>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                // Apply rate limiting
                using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
                if (!lease.IsAcquired)
                {
                    throw new GeminiException("Rate limit exceeded. Please try again later.");
                }

                var serializedContent = JsonSerializer.Serialize(data, _jsonOptions);
                var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async (ctx) =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = jsonContent
                    };
                    request.Headers.Add("X-Correlation-ID", correlationId);
                    return await _httpClient.SendAsync(request, cancellationToken);
                }, new Context());

                return await HandleResponse<TResponse>(response, correlationId, cancellationToken)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "POST request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
                throw new GeminiException("Failed to make POST request to Gemini API", ex);
            }
        }

        /// <summary>
        /// Handles the HTTP response and deserializes the content
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="response">The HTTP response message</param>
        /// <param name="correlationId">Correlation ID for request tracking</param>
        /// <returns>The deserialized response</returns>
        /// <exception cref="GeminiException">Thrown when the response indicates an error or cannot be deserialized</exception>
        private async Task<T> HandleResponse<T>(HttpResponseMessage response, string correlationId, CancellationToken cancellationToken = default)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                    if (result == null)
                    {
                        _logger.LogError("Response deserialization failed for type {Type} [ID: {CorrelationId}]", typeof(T).Name, correlationId);
                        throw new GeminiException($"Failed to deserialize response to type {typeof(T).Name}");
                    }
                    return result;
                }

                // Log API failure with response content
                _logger.LogError("API request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]", 
                    response.StatusCode, content, correlationId);

                var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions);
                if (geminiError?.error == null)
                {
                    throw new GeminiException($"Unexpected error response format. Status code: {response.StatusCode}");
                }

                throw new GeminiException(geminiError, geminiError.error.message)
                {
                    StatusCode = response.StatusCode
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse API response: {Content} [ID: {CorrelationId}]", content, correlationId);
                throw new GeminiException("Failed to parse API response", ex);
            }
        }

        /// <summary>
        /// Sends a streaming request to the Gemini API
        /// </summary>
        /// <typeparam name="TRequest">The type of the request data</typeparam>
        /// <param name="endpoint">The API endpoint to send the request to</param>
        /// <param name="data">The request data to send</param>
        /// <returns>An async enumerable of response text chunks</returns>
        public async IAsyncEnumerable<string> SendAsync<TRequest>(
            string endpoint,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();

            // Apply rate limiting
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiException("Rate limit exceeded. Please try again later.");
            }

            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, data, _jsonOptions, cancellationToken);
            ms.Seek(0, SeekOrigin.Begin);

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var requestContent = new StreamContent(ms);
            request.Content = requestContent;
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            request.Headers.Add("X-Correlation-ID", correlationId);
            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken));

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Stream request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                    response.StatusCode, content, correlationId);

                var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions)
                    ?? throw new GeminiException($"Failed to parse error response. Status code: {response.StatusCode}");

                throw new GeminiException(geminiError, geminiError.error.message)
                {
                    StatusCode = response.StatusCode
                };
            }

            var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var streamReader = new StreamReader(responseStream);
            var messageCount = 0;

            try
            {
                string? line;
                while ((line = await streamReader.ReadLineAsync()) is not null)
                {
                    if (!line.Contains(@"""text""")) continue;

                    string? processedText = null;
                    try
                    {
                        var jsonString = "{" + line + "}";
                        var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonString);
                        processedText = jsonObject?["text"]?.ToString();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse stream message: {Line} [ID: {CorrelationId}]", line, correlationId);
                        continue; // Skip malformed messages instead of failing the entire stream
                    }

                    if (processedText is not null)
                    {
                        messageCount++;
                        yield return processedText;
                    }
                }
            }
            finally
            {
                streamReader.Dispose();
                await responseStream.DisposeAsync();
                _logger.LogDebug("Stream completed with {MessageCount} messages [ID: {CorrelationId}]", messageCount, correlationId);
            }
        }
    }
}