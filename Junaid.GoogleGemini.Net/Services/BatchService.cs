using System.Text;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implements the Batch API. Uses a dedicated <see cref="HttpClient"/>
    /// (<see cref="GeminiHttpClients.Batches"/>) rather than the shared <c>IGeminiClient</c>: see that
    /// constant's doc comment for why (the interactive rate limiter and cost governor must not gate
    /// batch calls, and <c>IGeminiClient</c> has no way to opt out of either per-call).
    /// </summary>
    public class BatchService : IBatchService
    {
        private readonly HttpClient _httpClient;
        private readonly IFileService _fileService;
        private readonly ILogger<BatchService> _logger;
        private readonly JsonSerializerOptions _json = GeminiJson.Default;

        /// <summary>Initializes a new instance of the <see cref="BatchService"/>.</summary>
        public BatchService(IHttpClientFactory httpClientFactory, IFileService fileService, ILogger<BatchService> logger)
        {
            if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
            _httpClient = httpClientFactory.CreateClient(GeminiHttpClients.Batches);
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<BatchJob> CreateAsync(
            string model,
            IReadOnlyList<InlinedBatchRequest> requests,
            string? displayName = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateModelName(model);
            if (requests is null || requests.Count == 0)
            {
                throw new ArgumentException("At least one request is required.", nameof(requests));
            }

            var body = new CreateBatchRequest
            {
                Batch = new BatchCreatePayload
                {
                    DisplayName = displayName,
                    InputConfig = new BatchJobSource
                    {
                        Requests = new InlinedBatchRequestList { Requests = requests.ToList() }
                    }
                }
            };

            return PostAsync<CreateBatchRequest, BatchJob>(
                $"models/{model}:batchGenerateContent", body, "create batch job", cancellationToken);
        }

        /// <inheritdoc/>
        public Task<BatchJob> CreateFromFileAsync(
            string model,
            string fileName,
            string? displayName = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateModelName(model);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            var body = new CreateBatchRequest
            {
                Batch = new BatchCreatePayload
                {
                    DisplayName = displayName,
                    InputConfig = new BatchJobSource { FileName = NormalizeFileName(fileName) }
                }
            };

            return PostAsync<CreateBatchRequest, BatchJob>(
                $"models/{model}:batchGenerateContent", body, "create batch job from file", cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<BatchJob> CreateFromRequestsFileAsync(
            string model,
            IEnumerable<BatchRequestLine> requests,
            string? displayName = null,
            string? fileDisplayName = null,
            CancellationToken cancellationToken = default)
        {
            ValidationUtilities.ValidateModelName(model);
            if (requests is null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            // Build the JSONL payload: one compact JSON object per line, newline-delimited. NOT a
            // single JSON array. See docs/articles/batch-api.md for why this differs from every other
            // request shape in this library.
            var builder = new StringBuilder();
            var any = false;
            foreach (var line in requests)
            {
                if (line is null)
                {
                    continue;
                }
                builder.Append(JsonSerializer.Serialize(line, _json));
                builder.Append('\n');
                any = true;
            }

            if (!any)
            {
                throw new ArgumentException("At least one request is required.", nameof(requests));
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            var uploaded = await _fileService.UploadFileAsync(bytes, "jsonl", fileDisplayName, cancellationToken);
            if (string.IsNullOrWhiteSpace(uploaded.Name))
            {
                throw new GeminiSerializationException("The uploaded JSONL file has no resource name.");
            }

            return await CreateFromFileAsync(model, uploaded.Name!, displayName, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<BatchJob> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(Normalize(name), cancellationToken);
            await EnsureSuccessAsync(response, "get batch job", cancellationToken);

            var body = await response.Content.ReadStringAsync(cancellationToken);
            return Deserialize<BatchJob>(body)
                ?? throw new GeminiSerializationException($"No batch job returned for '{name}'.");
        }

        /// <inheritdoc/>
        public async Task<BatchJobList> ListAsync(
            int? pageSize = null,
            string? pageToken = null,
            CancellationToken cancellationToken = default)
        {
            var query = new List<string>();
            if (pageSize is > 0) query.Add($"pageSize={pageSize}");
            if (!string.IsNullOrEmpty(pageToken)) query.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
            var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

            using var response = await _httpClient.GetAsync($"batches{suffix}", cancellationToken);
            await EnsureSuccessAsync(response, "list batch jobs", cancellationToken);

            var body = await response.Content.ReadStringAsync(cancellationToken);
            return Deserialize<BatchJobList>(body) ?? new BatchJobList();
        }

        /// <inheritdoc/>
        public async Task CancelAsync(string name, CancellationToken cancellationToken = default)
        {
            // The response body's actual shape isn't reliably documented (see BatchJob.cs's history /
            // PLAN-batch-api.md §4.7), so it is deliberately not deserialized at all. Success or failure is
            // determined purely by EnsureSuccessAsync (status code), which is all CancelAsync promises.
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Normalize(name)}:cancel")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, "cancel batch job", cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync(Normalize(name), cancellationToken);
            await EnsureSuccessAsync(response, "delete batch job", cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<BatchJob> WaitUntilCompleteAsync(
            string name,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var interval = pollInterval ?? TimeSpan.FromSeconds(30);
            var deadline = timeout.HasValue ? DateTimeOffset.UtcNow + timeout.Value : (DateTimeOffset?)null;

            while (true)
            {
                var job = await GetAsync(name, cancellationToken);

                if (IsTerminalState(job.State))
                {
                    return job;
                }
                if (deadline.HasValue && DateTimeOffset.UtcNow >= deadline.Value)
                {
                    throw new GeminiTimeoutException($"Batch job '{name}' did not reach a terminal state before the timeout.");
                }

                await Task.Delay(interval, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<InlinedBatchResponse>> GetResultsAsync(BatchJob job, CancellationToken cancellationToken = default)
        {
            if (job is null)
            {
                throw new ArgumentNullException(nameof(job));
            }
            if (job.Output is null)
            {
                throw new GeminiException($"Batch job '{job.Name}' has no output yet (state: '{job.State}').");
            }

            if (job.Output.InlinedResponses?.InlinedResponses is { Count: > 0 } inline)
            {
                return inline;
            }

            if (!string.IsNullOrWhiteSpace(job.Output.ResponsesFile))
            {
                var bytes = await _fileService.DownloadFileAsync(job.Output.ResponsesFile!, cancellationToken);
                return ParseJsonl(bytes);
            }

            // Output is non-null, yet neither destination has anything in it. Deliberately an
            // exception, not a silent empty list: per this method's own contract (see
            // IBatchService.GetResultsAsync's doc comment) it's meant to be called on a completed job,
            // where "an Output object exists but is empty on both sides" is not a normal outcome.
            // Most likely a genuine job-level failure with truly no per-request output - check
            // BatchJob.Error first.
            throw new GeminiException(
                $"Batch job '{job.Name}' has an Output object but no inline responses and no results " +
                "file name. Check BatchJob.Error and BatchJob.State first: a job-level failure can " +
                "produce genuinely empty output.");
        }

        /// <summary>
        /// Whether a batch job <c>state</c> string represents a terminal outcome. Checked via an
        /// ordinal-insensitive suffix match, not an exact literal, since Google's own docs disagree with
        /// themselves on the exact prefix (<c>JOB_STATE_*</c> vs <c>BATCH_STATE_*</c>). See
        /// <c>PLAN-batch-api.md</c> §2.4. This keeps working whichever prefix the live API returns.
        /// Public (not just an internal implementation detail of <see cref="WaitUntilCompleteAsync"/>)
        /// since a caller polling <see cref="GetAsync"/> manually needs the same check.
        /// </summary>
        public static bool IsTerminalState(string? state)
        {
            // string.IsNullOrEmpty isn't annotated [NotNullWhen(false)] on netstandard2.0's older BCL
            // surface, so the compiler can't narrow `state` to non-null below on that TFM even though
            // it can on net8+, hence the explicit null check instead, which narrows on every TFM.
            if (state is null || state.Length == 0)
            {
                return false;
            }

            return state.EndsWith("SUCCEEDED", StringComparison.OrdinalIgnoreCase)
                || state.EndsWith("FAILED", StringComparison.OrdinalIgnoreCase)
                || state.EndsWith("CANCELLED", StringComparison.OrdinalIgnoreCase)
                || state.EndsWith("EXPIRED", StringComparison.OrdinalIgnoreCase);
        }

        private List<InlinedBatchResponse> ParseJsonl(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var results = new List<InlinedBatchResponse>();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r'); // tolerate CRLF-served content, not just LF
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                results.Add(Deserialize<InlinedBatchResponse>(line)
                    ?? throw new GeminiSerializationException("Failed to parse a batch results JSONL line."));
            }

            return results;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Batch job name is required.", nameof(name));
            }
            return name.StartsWith("batches/", StringComparison.Ordinal) ? name : $"batches/{name}";
        }

        private static string NormalizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("File name is required.", nameof(name));
            }
            return name.StartsWith("files/", StringComparison.Ordinal) ? name : $"files/{name}";
        }

        private async Task<TResponse> PostAsync<TRequest, TResponse>(
            string endpoint, TRequest body, string operation, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body, _json);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, operation, cancellationToken);

            var responseBody = await response.Content.ReadStringAsync(cancellationToken);
            return Deserialize<TResponse>(responseBody)
                ?? throw new GeminiSerializationException($"No response returned for '{operation}'.");
        }

        private T? Deserialize<T>(string body)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body, _json);
            }
            catch (JsonException ex)
            {
                throw new GeminiSerializationException("Failed to parse Batch API response.", ex);
            }
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadStringAsync(cancellationToken);
            _logger.LogError("Batch API {Operation} failed - Status: {StatusCode}, Body: {Body}",
                operation, response.StatusCode, body);

            ApiError? error = null;
            try
            {
                error = JsonSerializer.Deserialize<ApiErrorResponse>(body, _json)?.Error;
            }
            catch (JsonException)
            {
                // Non-JSON error body; fall through with a generic message.
            }

            throw new GeminiApiException(
                error?.Message ?? $"Batch API {operation} failed with status {(int)response.StatusCode}.",
                response.StatusCode,
                error);
        }
    }
}
