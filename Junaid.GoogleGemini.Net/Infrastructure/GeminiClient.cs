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
/// here</b>. They live on the <see cref="HttpClient"/> pipeline (configured in
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
        var (operation, model) = GeminiTelemetry.Parse(endpoint);
        using var activity = GeminiTelemetry.StartOperation(operation, model);
        var stopwatch = Stopwatch.StartNew();

        // `yield return` can't appear inside a try block that has a catch clause, so error handling
        // (to set the span's error status, same as PostAsync) has to live in an outer wrapper that
        // manually drives an inner enumerator, rather than directly wrapping the streaming logic below.
        var core = StreamCoreAsync(endpoint, data, correlationId, cancellationToken);
        await using var enumerator = core.GetAsyncEnumerator(cancellationToken);

        UsageMetadata? finalUsage = null;
        string? finalFinishReason = null;

        try
        {
            while (true)
            {
                GenerateContentResponse chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }
                    chunk = enumerator.Current;
                }
                catch (GeminiException)
                {
                    activity?.SetStatus(ActivityStatusCode.Error);
                    throw;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                    throw new GeminiTimeoutException($"The stream request to '{endpoint}' timed out.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    _logger.LogError(ex, "Stream request to {Endpoint} failed [ID: {CorrelationId}]", endpoint, correlationId);
                    throw new GeminiException("Failed to stream from Gemini API", ex);
                }

                finalUsage = chunk.Usage ?? finalUsage;
                finalFinishReason = chunk.FinishReason ?? finalFinishReason;
                yield return chunk;
            }
        }
        finally
        {
            // Same "only the final chunk carries real usage" reasoning as the cost-governance
            // recording in StreamCoreAsync below. Record the span/metric tags once, from the last
            // snapshot seen, not per-chunk (which would double-count the token histogram).
            GeminiTelemetry.RecordUsage(operation, model, finalUsage, finalFinishReason, activity);
            GeminiTelemetry.RecordDuration(operation, model, stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>The actual SSE streaming logic, driven by <see cref="StreamAsync{TRequest}"/> above.</summary>
    private async IAsyncEnumerable<GenerateContentResponse> StreamCoreAsync<TRequest>(
        string endpoint,
        TRequest data,
        string correlationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
            // Record spend from the final usage snapshot only. If the stream was cancelled or failed
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

    // ---- Interactions API (speech-to-text; see PLAN-stt.md) ----
    //
    // Not a reuse of PostAsync/StreamAsync above: the Interactions API's error body has a string
    // `code` (PostAsync's HandleResponse expects a numeric one and throws a JsonException on the
    // mismatch, discarding the real error message), its model name lives in the request body rather
    // than the URL (GeminiTelemetry.Parse has nothing to extract it from), and its streaming protocol
    // sends named events with different payload shapes per name rather than one response-shaped
    // `data:` line per chunk. See PLAN-stt.md §3.2-§3.4/§5.3 for the live verification behind this.

    /// <summary>Sends a request to the Interactions API.</summary>
    public async Task<Interaction> PostInteractionAsync(InteractionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var correlationId = Guid.NewGuid().ToString();
        const string operation = "interactions";
        var model = request.Model;
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

            var json = JsonSerializer.Serialize(request, typeof(InteractionRequest), _jsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "interactions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var result = await HandleInteractionResponse(response, correlationId, cancellationToken);

            var mappedUsage = MapUsage(result.Usage);
            GeminiTelemetry.RecordUsage(operation, model, mappedUsage, result.Status, activity);
            if (mappedUsage is not null)
            {
                _costGovernor.RecordSpend(model, mappedUsage);
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
            throw new GeminiTimeoutException("The POST request to 'interactions' timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "POST request to interactions failed [ID: {CorrelationId}]", correlationId);
            throw new GeminiException("Failed to make POST request to the Interactions API", ex);
        }
        finally
        {
            GeminiTelemetry.RecordDuration(operation, model, stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>Retrieves a previously created interaction by ID.</summary>
    public async Task<Interaction> GetInteractionAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Interaction ID is required.", nameof(id));
        }

        var correlationId = Guid.NewGuid().ToString();

        try
        {
            using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"interactions/{id}");
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await HandleInteractionResponse(response, correlationId, cancellationToken);
        }
        catch (GeminiException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiTimeoutException($"The GET request to 'interactions/{id}' timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "GET request to interactions/{Id} failed [ID: {CorrelationId}]", id, correlationId);
            throw new GeminiException("Failed to make GET request to the Interactions API", ex);
        }
    }

    /// <summary>Streams a request to the Interactions API, yielding each named SSE event.</summary>
    public async IAsyncEnumerable<InteractionStreamEvent> StreamInteractionAsync(
        InteractionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Clone rather than mutate the caller's request in place: GenerateAudioAsync/StreamAudioAsync
        // establish the pattern of filling in call-specific fields "on a cloned options object" (see
        // IGeminiService), specifically so a caller who reuses the same request instance elsewhere
        // (e.g. also passing it to PostInteractionAsync) isn't surprised by a side effect here.
        request = new InteractionRequest
        {
            Model = request.Model,
            Input = request.Input,
            GenerationConfig = request.GenerationConfig,
            Stream = true,
        };

        var correlationId = Guid.NewGuid().ToString();
        const string operation = "interactions";
        var model = request.Model;
        using var activity = GeminiTelemetry.StartOperation(operation, model);
        var stopwatch = Stopwatch.StartNew();

        // Same "yield return can't sit inside a try/catch" split as StreamAsync above: the actual SSE
        // read loop lives in StreamInteractionCoreAsync, driven manually here so this wrapper can add
        // exception mapping and span/duration telemetry around it.
        var core = StreamInteractionCoreAsync(request, correlationId, cancellationToken);
        await using var enumerator = core.GetAsyncEnumerator(cancellationToken);

        Interaction? finalInteraction = null;

        try
        {
            while (true)
            {
                InteractionStreamEvent evt;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }
                    evt = enumerator.Current;
                }
                catch (GeminiException)
                {
                    activity?.SetStatus(ActivityStatusCode.Error);
                    throw;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                    throw new GeminiTimeoutException("The stream request to 'interactions' timed out.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    _logger.LogError(ex, "Stream request to interactions failed [ID: {CorrelationId}]", correlationId);
                    throw new GeminiException("Failed to stream from the Interactions API", ex);
                }

                finalInteraction = evt.Interaction ?? finalInteraction;
                yield return evt;
            }
        }
        finally
        {
            GeminiTelemetry.RecordUsage(operation, model, MapUsage(finalInteraction?.Usage), finalInteraction?.Status, activity);
            GeminiTelemetry.RecordDuration(operation, model, stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>The actual SSE streaming logic for the Interactions API, driven by <see cref="StreamInteractionAsync"/>.</summary>
    private async IAsyncEnumerable<InteractionStreamEvent> StreamInteractionCoreAsync(
        InteractionRequest request,
        string correlationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = request.Model;

        using var lease = await _rateLimiter.AcquireAsync(cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new GeminiRateLimitException("Rate limit exceeded. Please try again later.");
        }

        _costGovernor.CheckBudget();

        var json = JsonSerializer.Serialize(request, typeof(InteractionRequest), _jsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "interactions?alt=sse")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadStringAsync(cancellationToken);
            _logger.LogError("Interactions stream request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                response.StatusCode, errorContent, correlationId);

            InteractionsApiErrorResponse? interactionsError;
            try
            {
                interactionsError = JsonSerializer.Deserialize<InteractionsApiErrorResponse>(errorContent, _jsonOptions);
            }
            catch (JsonException)
            {
                interactionsError = null;
            }

            throw new GeminiApiException(
                interactionsError?.Error?.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                interactionsError?.Error?.Code);
        }

        var responseStream = await response.Content.ReadStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);
        var eventCount = 0;
        string? currentEventType = null;
        var dataBuffer = new StringBuilder();
        Interaction? finalInteraction = null;

        try
        {
            while (true)
            {
                var line = await reader.ReadLineCancelableAsync(cancellationToken);
                if (line is null) break; // end of stream

                if (line.Length == 0)
                {
                    // A blank line terminates an SSE event; parse whatever we accumulated for it.
                    var evt = ParseInteractionEvent(currentEventType, dataBuffer, correlationId);
                    currentEventType = null;
                    dataBuffer.Clear();
                    if (evt is not null)
                    {
                        eventCount++;
                        finalInteraction = evt.Interaction ?? finalInteraction;
                        yield return evt;
                    }
                    continue;
                }

                // Unlike StreamCoreAsync above, we DO care about "event:" here: the payload shape on
                // "data:" differs by event name (interaction.created/completed carry a full
                // Interaction; step.delta carries incremental text; others carry neither).
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    currentEventType = line.Substring(6).Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    dataBuffer.Append(line.Substring(5).TrimStart());
                }
            }

            // Flush a trailing event that had no terminating blank line.
            var lastEvt = ParseInteractionEvent(currentEventType, dataBuffer, correlationId);
            if (lastEvt is not null)
            {
                eventCount++;
                finalInteraction = lastEvt.Interaction ?? finalInteraction;
                yield return lastEvt;
            }

            // Same "only record real, confirmed usage; never a guessed partial cost on a
            // cancelled/failed stream" reasoning as StreamCoreAsync above.
            var mappedUsage = MapUsage(finalInteraction?.Usage);
            if (mappedUsage is not null)
            {
                _costGovernor.RecordSpend(model, mappedUsage);
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
            _logger.LogDebug("Interactions stream completed with {EventCount} events [ID: {CorrelationId}]", eventCount, correlationId);
        }
    }

    /// <summary>Parses one Interactions API SSE event; returns null for a "done" sentinel or malformed data.</summary>
    private InteractionStreamEvent? ParseInteractionEvent(string? eventType, StringBuilder dataBuffer, string correlationId)
    {
        if (dataBuffer.Length == 0) return null;

        var payload = dataBuffer.ToString();
        if (payload == "[DONE]") return null; // The literal "done" event's data, not a JSON object.

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var evt = new InteractionStreamEvent { EventType = eventType };

            if (root.TryGetProperty("interaction", out var interactionElement))
            {
                evt.Interaction = JsonSerializer.Deserialize<Interaction>(interactionElement.GetRawText(), _jsonOptions);
            }

            if (root.TryGetProperty("delta", out var deltaElement)
                && deltaElement.TryGetProperty("type", out var deltaTypeElement)
                && deltaTypeElement.ValueEquals("text")
                && deltaElement.TryGetProperty("text", out var deltaTextElement))
            {
                evt.DeltaText = deltaTextElement.GetString();
            }

            return evt;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Interactions stream event: {Payload} [ID: {CorrelationId}]", payload, correlationId);
            return null; // Skip malformed events rather than failing the whole stream.
        }
    }

    /// <summary>Deserializes a success response or maps an Interactions API error to a typed exception.</summary>
    private async Task<Interaction> HandleInteractionResponse(HttpResponseMessage response, string correlationId, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadStringAsync(cancellationToken);

        try
        {
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<Interaction>(content, _jsonOptions);
                if (result == null)
                {
                    _logger.LogError("Response deserialization returned null for type Interaction [ID: {CorrelationId}]", correlationId);
                    throw new GeminiSerializationException("Failed to deserialize response to type Interaction");
                }
                return result;
            }

            _logger.LogError("Interactions API request failed - Status: {StatusCode}, Response: {ResponseContent} [ID: {CorrelationId}]",
                response.StatusCode, content, correlationId);

            var interactionsError = JsonSerializer.Deserialize<InteractionsApiErrorResponse>(content, _jsonOptions);
            if (interactionsError?.Error == null)
            {
                throw new GeminiApiException(
                    $"Request failed with status {(int)response.StatusCode} and an unexpected error format.",
                    response.StatusCode,
                    (string?)null);
            }

            throw new GeminiApiException(
                interactionsError.Error.Message ?? $"Request failed with status {(int)response.StatusCode}",
                response.StatusCode,
                interactionsError.Error.Code);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Interactions API response: {Content} [ID: {CorrelationId}]", content, correlationId);
            throw new GeminiSerializationException("Failed to parse Interactions API response", ex);
        }
    }

    /// <summary>
    /// Maps an <see cref="Interaction"/>'s usage to generateContent's <see cref="UsageMetadata"/>
    /// shape, so the existing cost-governance (<see cref="ICostGovernor.RecordSpend"/>) and telemetry
    /// (<see cref="GeminiTelemetry.RecordUsage"/>) machinery can be reused unchanged. CONFIRMED LIVE
    /// (PLAN-stt.md §3.3): <see cref="InteractionUsage.TotalOutputTokens"/> reads 0 even when real
    /// output text was produced; <see cref="UsageMetadata.CandidatesTokenCount"/> below is summed from
    /// <see cref="InteractionUsage.ModelInvocationTokenCounts"/> instead, which is where the real
    /// count lives.
    ///
    /// NOT independently confirmed: whether <see cref="InteractionUsage.TotalCachedTokens"/> is a
    /// subset of <see cref="InteractionUsage.TotalInputTokens"/> (as <c>generateContent</c>'s
    /// equivalent fields are, which is what <see cref="GeminiCostGovernor.ComputeCost"/>'s
    /// <c>PromptTokenCount - CachedContentTokenCount</c> subtraction assumes) or a separate, additive
    /// figure. Every live call made while building this feature had <c>TotalCachedTokens == 0</c>
    /// (transcription requests have no way to attach cached content today), so this was never
    /// actually exercised. Mapped here on the same subset assumption as everywhere else in this
    /// library; re-verify live before trusting cost governance for a transcription request that
    /// somehow involves cached tokens.
    /// </summary>
    private static UsageMetadata? MapUsage(InteractionUsage? usage)
    {
        if (usage is null) return null;

        var candidatesDetails = usage.ModelInvocationTokenCounts?
            .SelectMany(m => m.CandidatesTokensDetails ?? new List<InteractionModalityTokenCount>())
            .ToList();
        var candidatesTokenCount = candidatesDetails?.Sum(d => d.Tokens) ?? 0;

        return new UsageMetadata
        {
            PromptTokenCount = usage.TotalInputTokens,
            CandidatesTokenCount = candidatesTokenCount,
            // Matches generateContent's real totalTokenCount = prompt + candidates + thoughts
            // (confirmed live, e.g. a plain-text sanity call this session: 8 + 2 + 96 == 106).
            TotalTokenCount = usage.TotalInputTokens + candidatesTokenCount + usage.TotalThoughtTokens,
            ThoughtsTokenCount = usage.TotalThoughtTokens,
            CachedContentTokenCount = usage.TotalCachedTokens,
            PromptTokensDetails = usage.InputTokensByModality?
                .Select(m => new ModalityTokenCount { Modality = m.Modality, TokenCount = m.Tokens })
                .ToList(),
            CandidatesTokensDetails = candidatesDetails?
                .Select(d => new ModalityTokenCount { Modality = d.Modality, TokenCount = d.Tokens })
                .ToList(),
        };
    }
}
