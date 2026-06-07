using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>
/// Low-level HTTP client for the Google Gemini API.
/// </summary>
/// <remarks>
/// This type is intentionally thin: it builds requests, applies client-side rate limiting, sends,
/// and maps responses/errors to typed results. <b>Retries, backoff and timeouts are NOT handled
/// here</b> — they live on the <see cref="HttpClient"/> pipeline (configured in
/// <c>GeminiExtensions.AddGemini</c> via the standard resilience handler). That separation is what
/// fixes the previous retry bug: the resilience handler re-sends a buffered request internally, so
/// we never reuse a disposed <see cref="HttpContent"/> across attempts.
/// </remarks>
public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private readonly IRateLimiter _rateLimiter;
    private readonly JsonSerializerOptions _jsonOptions = GeminiJson.Default;

    /// <summary>Initializes a new instance of the <see cref="GeminiClient"/>.</summary>
    /// <param name="httpClient">The configured HttpClient (resilience + auth handlers attached by DI).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="rateLimiter">Client-side rate limiter.</param>
    public GeminiClient(
        HttpClient httpClient,
        ILogger<GeminiClient> logger,
        IRateLimiter rateLimiter)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    /// <summary>Sends a GET request and deserializes the response.</summary>
    /// <param name="endpoint">The API endpoint (relative to the configured base address).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    public async Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await HandleResponse<TResponse>(response, correlationId, cancellationToken)
                   ?? throw new GeminiException("The API returned a null response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The token wasn't tripped by the caller, so this is a timeout, not a cancellation.
            throw new GeminiTimeoutException($"The GET request to '{endpoint}' timed out.");
        }
        catch (Exception ex) when (ex is not GeminiException and not OperationCanceledException)
        {
            _logger.LogError(ex, "GET request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
            throw new GeminiException("Failed to make GET request to Gemini API", ex);
        }
    }

    /// <summary>Sends a POST request with a JSON body and deserializes the response.</summary>
    /// <param name="endpoint">The API endpoint (relative to the configured base address).</param>
    /// <param name="data">The request payload.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
            }

            var json = JsonSerializer.Serialize(data, typeof(TRequest), _jsonOptions);

            // The content is buffered (ByteArrayContent), so the resilience handler can re-send it on retry.
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await HandleResponse<TResponse>(response, correlationId, cancellationToken)
                   ?? throw new GeminiException("The API returned a null response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiTimeoutException($"The POST request to '{endpoint}' timed out.");
        }
        catch (Exception ex) when (ex is not GeminiException and not OperationCanceledException)
        {
            _logger.LogError(ex, "POST request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
            throw new GeminiException("Failed to make POST request to Gemini API", ex);
        }
    }

    /// <summary>Deserializes a success response or maps an error response to a typed exception.</summary>
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
                    _logger.LogError("Response deserialization returned null for type {Type} [ID: {CorrelationId}]", typeof(T).Name, correlationId);
                    throw new GeminiSerializationException($"Failed to deserialize response to type {typeof(T).Name}");
                }
                return result;
            }

            _logger.LogError("API request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                response.StatusCode, content, correlationId);

            var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions);
            if (geminiError?.Error == null)
            {
                throw new GeminiApiException(
                    $"Request failed with status {(int)response.StatusCode} and an unexpected error format.",
                    response.StatusCode);
            }

            throw new GeminiApiException(
                geminiError.Error.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                geminiError.Error);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse API response: {Content} [ID: {CorrelationId}]", content, correlationId);
            throw new GeminiSerializationException("Failed to parse API response", ex);
        }
    }

    /// <summary>
    /// Sends a streaming request and yields text chunks as they arrive.
    /// </summary>
    /// <remarks>
    /// Phase 1, Step 4 replaces this line-sniffing parser with a proper Server-Sent-Events reader
    /// (<c>?alt=sse</c>) that surfaces structured chunks. For now it remains a text-only stream but
    /// goes through the same resilient pipeline and honors cancellation.
    /// </remarks>
    public async IAsyncEnumerable<string> SendAsync<TRequest>(
        string endpoint,
        TRequest data,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
        }

        var json = JsonSerializer.Serialize(data, typeof(TRequest), _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Stream request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                response.StatusCode, content, correlationId);

            var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions);
            throw new GeminiApiException(
                geminiError?.Error?.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                geminiError?.Error);
        }

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var streamReader = new StreamReader(responseStream);
        var messageCount = 0;

        try
        {
            string? line;
            while ((line = await streamReader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (!line.Contains(@"""text""", StringComparison.Ordinal)) continue;

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
                    continue; // Skip malformed messages instead of failing the entire stream.
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
