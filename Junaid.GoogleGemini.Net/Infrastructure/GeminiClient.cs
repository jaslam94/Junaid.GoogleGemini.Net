using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net;
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

        /// <summary>
        /// Initializes a new instance of the GeminiClient
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for API requests</param>
        /// <param name="logger">Logger for diagnostic information</param>
        public GeminiClient(HttpClient httpClient, ILogger<GeminiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                                "Retry {RetryCount} after {RetryTime}ms due to {StatusCode}",
                                retryCount,
                                timeSpan.TotalMilliseconds,
                                exception.Result.StatusCode);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Retry {RetryCount} after {RetryTime}ms due to {Exception}",
                                retryCount,
                                timeSpan.TotalMilliseconds,
                                exception.Exception.Message);
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
        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                _logger.LogInformation("Making GET request to endpoint: {Endpoint}", endpoint);
                var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(endpoint));
                return await HandleResponse<TResponse>(response)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "Error making GET request to {Endpoint}", endpoint);
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
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                _logger.LogInformation("Making POST request to endpoint: {Endpoint}", endpoint);
                var serializedContent = JsonSerializer.Serialize(data, _jsonOptions);
                var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Request payload: {Payload}", serializedContent);
                var response = await _retryPolicy.ExecuteAsync(async () => 
                    await _httpClient.PostAsync(endpoint, jsonContent));
                
                return await HandleResponse<TResponse>(response)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "Error making POST request to {Endpoint}", endpoint);
                throw new GeminiException("Failed to make POST request to Gemini API", ex);
            }
        }

        /// <summary>
        /// Handles the HTTP response and deserializes the content
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="response">The HTTP response message</param>
        /// <returns>The deserialized response</returns>
        /// <exception cref="GeminiException">Thrown when the response indicates an error or cannot be deserialized</exception>
        private async Task<T> HandleResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            
            try
            {
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successful response received. Status code: {StatusCode}", response.StatusCode);
                    _logger.LogTrace("Response content: {Content}", content);

                    var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                    if (result == null)
                    {
                        _logger.LogError("Deserialization resulted in null object for type {Type}", typeof(T).Name);
                        throw new GeminiException($"Failed to deserialize response to type {typeof(T).Name}");
                    }
                    return result;
                }
                
                _logger.LogWarning("API returned error response. Status code: {StatusCode}", response.StatusCode);
                _logger.LogDebug("Error response content: {Content}", content);

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
                _logger.LogError(ex, "Failed to parse response content: {Content}", content);
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
        public async IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data)
        {
            _logger.LogInformation("Starting streaming request to endpoint: {Endpoint}", endpoint);
            
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, data, _jsonOptions);
            ms.Seek(0, SeekOrigin.Begin);

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            using var requestContent = new StreamContent(ms);
            request.Content = requestContent;
            requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            _logger.LogDebug("Sending streaming request with payload size: {Size} bytes", ms.Length);
            
            var response = await _retryPolicy.ExecuteAsync(async () => 
                await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead));

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Stream request failed. Status: {StatusCode}. Content: {Content}", 
                    response.StatusCode, content);
                    
                var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions)
                    ?? throw new GeminiException($"Failed to parse error response. Status code: {response.StatusCode}");
                    
                throw new GeminiException(geminiError, geminiError.error.message)
                {
                    StatusCode = response.StatusCode
                };
            }

            _logger.LogInformation("Stream connection established successfully");
            var responseStream = await response.Content.ReadAsStreamAsync();
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
                        _logger.LogError(ex, "Failed to parse stream message: {Line}", line);
                        continue; // Skip malformed messages instead of failing the entire stream
                    }

                    if (processedText is not null)
                    {
                        messageCount++;
                        _logger.LogDebug("Received message {Count}: {Length} characters", 
                            messageCount, processedText.Length);
                        yield return processedText;
                    }
                }
            }
            finally
            {
                streamReader.Dispose();
                await responseStream.DisposeAsync();
                _logger.LogInformation("Stream completed. Total messages: {Count}", messageCount);
            }
        }
        }
    }
}