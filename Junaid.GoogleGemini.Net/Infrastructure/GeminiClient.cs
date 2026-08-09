using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
    private readonly ICostGovernor _costGovernor;
    private readonly JsonSerializerOptions _jsonOptions = GeminiJson.Default;

    /// <summary>Initializes a new instance of the <see cref="GeminiClient"/>.</summary>
    /// <param name="httpClient">The configured HttpClient (resilience + auth handlers attached by DI).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="rateLimiter">Client-side rate limiter.</param>
    /// <param name="costGovernor">Cost governance: pre-flight budget check + post-call spend recording.</param>
    public GeminiClient(
        HttpClient httpClient,
        ILogger<GeminiClient> logger,
        IRateLimiter rateLimiter,
        ICostGovernor costGovernor)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _costGovernor = costGovernor ?? throw new ArgumentNullException(nameof(costGovernor));
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
        var (operation, model) = GeminiTelemetry.Parse(endpoint);
        using var activity = GeminiTelemetry.StartOperation(operation, model);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
            }

            _costGovernor.CheckBudget();

            var json = JsonSerializer.Serialize(data, typeof(TRequest), _jsonOptions);

            // The content is buffered (ByteArrayContent), so the resilience handler can re-send it on retry.
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = await HandleResponse<TResponse>(response, correlationId, cancellationToken)
                   ?? throw new GeminiException("The API returned a null response.");

            if (result is GenerateContentResponse contentResponse)
            {
                GeminiTelemetry.RecordUsage(operation, model, contentResponse.Usage, contentResponse.FinishReason, activity);
            }

            if (result is GenerateContentResponse { Usage: not null } usageResponse)
            {
                _costGovernor.RecordSpend(model, usageResponse.Usage!);
            }

            return result;
        }
        catch (GeminiException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "timeout");
            throw new GeminiTimeoutException($"The POST request to '{endpoint}' timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "POST request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
            throw new GeminiException("Failed to make POST request to Gemini API", ex);
        }
        finally
        {
            GeminiTelemetry.RecordDuration(operation, model, stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>Sends a PATCH request with a JSON body and deserializes the response.</summary>
    public async Task<TResponse> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
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
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint)
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
            throw new GeminiTimeoutException($"The PATCH request to '{endpoint}' timed out.");
        }
        catch (Exception ex) when (ex is not GeminiException and not OperationCanceledException)
        {
            _logger.LogError(ex, "PATCH request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
            throw new GeminiException("Failed to make PATCH request to Gemini API", ex);
        }
    }

    /// <summary>Sends a DELETE request, mapping a failure response to a typed exception.</summary>
    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var content = await response.Content.ReadStringAsync(cancellationToken);
            var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(content, _jsonOptions);
            throw new GeminiApiException(
                geminiError?.Error?.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                geminiError?.Error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiTimeoutException($"The DELETE request to '{endpoint}' timed out.");
        }
        catch (Exception ex) when (ex is not GeminiException and not OperationCanceledException)
        {
            _logger.LogError(ex, "DELETE request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
            throw new GeminiException("Failed to make DELETE request to Gemini API", ex);
        }
    }

    /// <summary>Deserializes a success response or maps an error response to a typed exception.</summary>
    private async Task<T> HandleResponse<T>(HttpResponseMessage response, string correlationId, CancellationToken cancellationToken = default)
    {
        var content = await response.Content.ReadStringAsync(cancellationToken);

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
    /// Streams a generate-content request over Server-Sent Events, yielding each response chunk.
    /// </summary>
    /// <remarks>
    /// The endpoint must request SSE (<c>...:streamGenerateContent?alt=sse</c>). The server sends one
    /// <c>data: {GenerateContentResponse}</c> event per chunk, separated by blank lines; we parse each
    /// event into a full <see cref="GenerateContentResponse"/>. Malformed events are logged and
    /// skipped rather than tearing down the whole stream.
    /// </remarks>
    public async IAsyncEnumerable<GenerateContentResponse> StreamAsync<TRequest>(
        string endpoint,
        TRequest data,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        // Only needed to attribute cost-governance spend to a model (see below) — StreamAsync
        // otherwise carries no telemetry today (a pre-existing, separately tracked gap; not
        // addressed here beyond what cost governance needs).
        var (_, model) = GeminiTelemetry.Parse(endpoint);

        using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
        }

        _costGovernor.CheckBudget();

        var json = JsonSerializer.Serialize(data, typeof(TRequest), _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadStringAsync(cancellationToken);
            _logger.LogError("Stream request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                response.StatusCode, errorContent, correlationId);

            var geminiError = JsonSerializer.Deserialize<ApiErrorResponse>(errorContent, _jsonOptions);
            throw new GeminiApiException(
                geminiError?.Error?.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                geminiError?.Error);
        }

        var responseStream = await response.Content.ReadStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);
        var chunkCount = 0;
        var dataBuffer = new StringBuilder();
        // Gemini's SSE stream carries usageMetadata on the FINAL chunk only (intermediate chunks
        // typically omit it or carry partial data), so track the most recent non-null sighting.
        UsageMetadata? finalUsage = null;

        try
        {
            while (true)
            {
                var line = await reader.ReadLineCancelableAsync(cancellationToken);
                if (line is null) break; // end of stream

                if (line.Length == 0)
                {
                    // A blank line terminates an SSE event; parse whatever data we accumulated.
                    var chunk = ParseEvent(dataBuffer, correlationId);
                    dataBuffer.Clear();
                    if (chunk is not null)
                    {
                        chunkCount++;
                        yield return chunk;
                        finalUsage = chunk.UsageMetadata ?? finalUsage;
                    }
                    continue;
                }

                // We only care about the "data:" field; ignore comments (":..."), "event:", "id:", etc.
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    dataBuffer.Append(line.Substring(5).TrimStart());
                }
            }

            // Flush a trailing event that had no terminating blank line.
            var lastChunk = ParseEvent(dataBuffer, correlationId);
            if (lastChunk is not null)
            {
                chunkCount++;
                yield return lastChunk;
                finalUsage = lastChunk.UsageMetadata ?? finalUsage;
            }

            // The stream completed successfully (no cancellation/exception broke out of the try above).
            // Record spend from the final usage snapshot only — if the stream was cancelled or failed
            // partway through, we deliberately do NOT record a guessed partial cost: Gemini bills for
            // what the server actually generated regardless of whether the client kept reading, but
            // this library has no way to know the true billed amount without the final usageMetadata,
            // so a partial figure would be worse than recording nothing. (Known limitation: a cancelled
            // stream's actual cost won't be reflected in the tracked total.)
            if (finalUsage is not null)
            {
                _costGovernor.RecordSpend(model, finalUsage);
            }
        }
        finally
        {
            reader.Dispose();
#if NET8_0_OR_GREATER
            await responseStream.DisposeAsync();
#else
            responseStream.Dispose();
#endif
            _logger.LogDebug("Stream completed with {ChunkCount} chunks [ID: {CorrelationId}]", chunkCount, correlationId);
        }
    }

    /// <summary>Parses one SSE event's accumulated data into a response chunk; returns null to skip.</summary>
    private GenerateContentResponse? ParseEvent(StringBuilder dataBuffer, string correlationId)
    {
        if (dataBuffer.Length == 0) return null;

        var payload = dataBuffer.ToString();
        if (payload == "[DONE]") return null; // Some servers emit a sentinel; Gemini doesn't, but be safe.

        try
        {
            return JsonSerializer.Deserialize<GenerateContentResponse>(payload, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse stream event: {Payload} [ID: {CorrelationId}]", payload, correlationId);
            return null; // Skip malformed events rather than failing the whole stream.
        }
    }
}
