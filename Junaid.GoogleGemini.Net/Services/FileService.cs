using System.Text;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Junaid.GoogleGemini.Net.Services
{
    /// <summary>
    /// Implements the Gemini Files API. Uses a dedicated HttpClient (<see cref="GeminiHttpClients.Files"/>)
    /// because uploads target the <c>/upload</c> path and a server-issued absolute session URL, which
    /// don't fit the versioned base address used for normal calls.
    /// </summary>
    public class FileService : IFileService
    {
        private const string Version = "v1beta";

        private readonly HttpClient _httpClient;
        private readonly ILogger<FileService> _logger;
        private readonly JsonSerializerOptions _json = GeminiJson.Default;

        /// <summary>Initializes a new instance of the <see cref="FileService"/>.</summary>
        public FileService(IHttpClientFactory httpClientFactory, ILogger<FileService> logger)
        {
            if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
            _httpClient = httpClientFactory.CreateClient(GeminiHttpClients.Files);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<FileResource> UploadFileAsync(
            byte[] content,
            string mimeType,
            string? displayName = null,
            CancellationToken cancellationToken = default)
        {
            if (content is null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentException("MIME type is required.", nameof(mimeType));
            }

            // Step 1: start a resumable session. The server returns an upload URL in a header.
            using var startRequest = new HttpRequestMessage(HttpMethod.Post, $"upload/{Version}/files");
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "start");
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Length", content.Length.ToString());
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Header-Content-Type", mimeType);

            var metadata = new FileUploadStartRequest { File = new FileUploadMetadata { DisplayName = displayName } };
            startRequest.Content = new StringContent(JsonSerializer.Serialize(metadata, _json), Encoding.UTF8, "application/json");

            using var startResponse = await _httpClient.SendAsync(startRequest, cancellationToken);
            await EnsureSuccessAsync(startResponse, "start file upload", cancellationToken);

            if (!startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var uploadUrls))
            {
                throw new GeminiException("The Files API did not return an upload URL.");
            }
            var uploadUrl = uploadUrls.First();

            // Step 2: upload the bytes and finalize in one call.
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", "0");
            uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");
            uploadRequest.Content = new ByteArrayContent(content);

            using var uploadResponse = await _httpClient.SendAsync(uploadRequest, cancellationToken);
            await EnsureSuccessAsync(uploadResponse, "finalize file upload", cancellationToken);

            var body = await uploadResponse.Content.ReadStringAsync(cancellationToken);
            var parsed = Deserialize<FileUploadResponse>(body);
            return parsed?.File
                ?? throw new GeminiSerializationException("The upload response did not contain a file.");
        }

        /// <inheritdoc/>
        public async Task<FileResource> GetFileAsync(string name, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"{Version}/{Normalize(name)}", cancellationToken);
            await EnsureSuccessAsync(response, "get file", cancellationToken);

            var body = await response.Content.ReadStringAsync(cancellationToken);
            return Deserialize<FileResource>(body)
                ?? throw new GeminiSerializationException($"No metadata returned for file '{name}'.");
        }

        /// <inheritdoc/>
        public async Task<FileListResponse> ListFilesAsync(
            int? pageSize = null,
            string? pageToken = null,
            CancellationToken cancellationToken = default)
        {
            var query = new List<string>();
            if (pageSize is > 0) query.Add($"pageSize={pageSize}");
            if (!string.IsNullOrEmpty(pageToken)) query.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
            var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

            using var response = await _httpClient.GetAsync($"{Version}/files{suffix}", cancellationToken);
            await EnsureSuccessAsync(response, "list files", cancellationToken);

            var body = await response.Content.ReadStringAsync(cancellationToken);
            return Deserialize<FileListResponse>(body) ?? new FileListResponse();
        }

        /// <inheritdoc/>
        public async Task DeleteFileAsync(string name, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"{Version}/{Normalize(name)}", cancellationToken);
            await EnsureSuccessAsync(response, "delete file", cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(
                $"download/{Version}/{Normalize(name)}:download?alt=media", cancellationToken);
            await EnsureSuccessAsync(response, "download file", cancellationToken);
            return await response.Content.ReadBytesAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<FileResource> WaitUntilActiveAsync(
            string name,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var interval = pollInterval ?? TimeSpan.FromSeconds(2);
            var deadline = timeout.HasValue ? DateTimeOffset.UtcNow + timeout.Value : (DateTimeOffset?)null;

            while (true)
            {
                var file = await GetFileAsync(name, cancellationToken);

                if (string.Equals(file.State, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
                if (string.Equals(file.State, "FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    throw new GeminiException($"File '{name}' failed processing.");
                }
                if (deadline.HasValue && DateTimeOffset.UtcNow >= deadline.Value)
                {
                    throw new GeminiTimeoutException($"File '{name}' did not become ACTIVE before the timeout.");
                }

                await Task.Delay(interval, cancellationToken);
            }
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("File name is required.", nameof(name));
            }
            return name.StartsWith("files/", StringComparison.Ordinal) ? name : $"files/{name}";
        }

        private T? Deserialize<T>(string body)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body, _json);
            }
            catch (JsonException ex)
            {
                throw new GeminiSerializationException("Failed to parse Files API response.", ex);
            }
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadStringAsync(cancellationToken);
            _logger.LogError("Files API {Operation} failed - Status: {StatusCode}, Body: {Body}",
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
                error?.Message ?? $"Files API {operation} failed with status {(int)response.StatusCode}.",
                response.StatusCode,
                error);
        }
    }
}
