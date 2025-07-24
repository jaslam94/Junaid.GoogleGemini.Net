using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    public class GeminiClient : IGeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public GeminiClient(HttpClient httpClient, ILogger<GeminiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                _logger.LogInformation("Making GET request to endpoint: {Endpoint}", endpoint);
                var response = await _httpClient.GetAsync(endpoint);
                return await HandleResponse<TResponse>(response)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "Error making GET request to {Endpoint}", endpoint);
                throw new GeminiException("Failed to make GET request to Gemini API", ex);
            }
        }

        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                _logger.LogInformation("Making POST request to endpoint: {Endpoint}", endpoint);
                var serializedContent = JsonSerializer.Serialize(data, _jsonOptions);
                var jsonContent = new StringContent(serializedContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Request payload: {Payload}", serializedContent);
                var response = await _httpClient.PostAsync(endpoint, jsonContent);
                
                return await HandleResponse<TResponse>(response)
                       ?? throw new GeminiException("The API has returned a null response.");
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "Error making POST request to {Endpoint}", endpoint);
                throw new GeminiException("Failed to make POST request to Gemini API", ex);
            }
        }

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

        public async IAsyncEnumerable<string> SendAsync<TRequest>(string endpoint, TRequest data)
        {
            _logger.LogInformation("Starting streaming request to endpoint: {Endpoint}", endpoint);
            
            try
            {
                using var ms = new MemoryStream();
                await JsonSerializer.SerializeAsync(ms, data, _jsonOptions);
                ms.Seek(0, SeekOrigin.Begin);

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                using var requestContent = new StreamContent(ms);
                request.Content = requestContent;
                requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                
                _logger.LogDebug("Sending streaming request with payload size: {Size} bytes", ms.Length);
                
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Stream connection established successfully");
                    using var responseStream = await response.Content.ReadAsStreamAsync();
                    using var streamReader = new StreamReader(responseStream);
                    
                    int messageCount = 0;
                    string? line;
                    while ((line = await streamReader.ReadLineAsync()) is not null)
                    {
                        if (line.Contains(@"""text"""))
                        {
                            try
                            {
                                var jsonString = "{" + line + "}";
                                var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonString);
                                var text = jsonObject?["text"]?.ToString();
                                
                                if (text is not null)
                                {
                                    messageCount++;
                                    _logger.LogDebug("Received message {Count}: {Length} characters", 
                                        messageCount, text.Length);
                                    yield return text;
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogError(ex, "Failed to parse stream message: {Line}", line);
                                throw new GeminiException("Failed to parse stream message", ex);
                            }
                        }
                    }
                    
                    _logger.LogInformation("Stream completed. Total messages: {Count}", messageCount);
                }
                else
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
            }
            catch (Exception ex) when (ex is not GeminiException)
            {
                _logger.LogError(ex, "Unexpected error during streaming request to {Endpoint}", endpoint);
                throw new GeminiException("Failed to process streaming request", ex);
            }
        }
    }
}