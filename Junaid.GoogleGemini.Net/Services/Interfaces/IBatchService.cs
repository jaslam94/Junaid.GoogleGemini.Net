using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Submits and manages Batch API jobs: large volumes of <c>generateContent</c> requests processed
    /// asynchronously at Google's discounted batch rate (see <c>docs/articles/batch-api.md</c>). A
    /// batch job is create-then-poll-then-fetch, a fundamentally different usage pattern from the
    /// request/response calls on <see cref="IGeminiService"/>, which is why this is its own resource
    /// client rather than a facade method — the same reasoning behind <see cref="IFileService"/> and
    /// <see cref="ICachingService"/> being separate services too.
    /// </summary>
    public interface IBatchService
    {
        /// <summary>
        /// Creates a batch job from inline requests. Google's documented ceiling is under ~20MB total
        /// request size; for anything larger, use <see cref="CreateFromFileAsync"/> or
        /// <see cref="CreateFromRequestsFileAsync"/> instead.
        /// </summary>
        /// <param name="model">The model to run every request against, e.g. "gemini-3.6-flash".</param>
        /// <param name="requests">The requests to run. Must be non-empty.</param>
        /// <param name="displayName">Optional human-readable display name for the job.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<BatchJob> CreateAsync(
            string model,
            IReadOnlyList<InlinedBatchRequest> requests,
            string? displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a batch job from a previously uploaded JSONL file (see
        /// <see cref="IFileService.UploadFileAsync"/>). Use this when you already have a JSONL file
        /// (e.g. built by an external pipeline); if you have an in-memory list of requests instead, use
        /// <see cref="CreateFromRequestsFileAsync"/> so you don't have to hand-write the JSONL yourself.
        /// </summary>
        /// <param name="model">The model to run every request against, e.g. "gemini-3.6-flash".</param>
        /// <param name="fileName">The uploaded JSONL file's resource name (e.g. "files/abc123").</param>
        /// <param name="displayName">Optional human-readable display name for the job.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<BatchJob> CreateFromFileAsync(
            string model,
            string fileName,
            string? displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience wrapper: writes <paramref name="requests"/> as a JSONL file, uploads it via
        /// <see cref="IFileService.UploadFileAsync"/>, then calls <see cref="CreateFromFileAsync"/>.
        /// Exists so a caller with an in-memory list of requests never has to hand-write the JSONL
        /// protocol themselves — the file-mode equivalent of what <see cref="CreateAsync"/> already
        /// gives inline callers for free. Recommended over <see cref="CreateAsync"/> for large volumes
        /// (file mode's ceiling is ~2GB vs inline's ~20MB).
        /// </summary>
        /// <param name="model">The model to run every request against, e.g. "gemini-3.6-flash".</param>
        /// <param name="requests">The requests to run. Must be non-empty.</param>
        /// <param name="displayName">Optional human-readable display name for the job.</param>
        /// <param name="fileDisplayName">Optional display name for the uploaded JSONL file itself.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<BatchJob> CreateFromRequestsFileAsync(
            string model,
            IEnumerable<BatchRequestLine> requests,
            string? displayName = null,
            string? fileDisplayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>Gets a batch job's current status by resource name ("batches/123" or "123").</summary>
        Task<BatchJob> GetAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>Lists batch jobs.</summary>
        Task<BatchJobList> ListAsync(
            int? pageSize = null,
            string? pageToken = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests cancellation of a running/pending batch job. Cancellation is asynchronous on
        /// Google's side too — call <see cref="GetAsync"/> afterward to observe the actual state
        /// transition, don't assume it's cancelled the instant this call returns.
        /// </summary>
        Task CancelAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>Deletes a batch job and its results.</summary>
        Task DeleteAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls <see cref="GetAsync"/> until the job reaches a terminal state (succeeded, failed,
        /// cancelled, or expired — see <see cref="BatchJob.State"/>'s remarks for why this is checked
        /// via a suffix match rather than an exact literal). Google's own target turnaround is "24
        /// hours, often faster," with no hard SLA and up to 48 hours before a job auto-expires — pick a
        /// <paramref name="timeout"/> that reflects that; there is no sensible default here, so leaving
        /// it unset means this can legitimately run for hours.
        /// </summary>
        /// <param name="name">The batch job's resource name.</param>
        /// <param name="pollInterval">How often to re-check. Defaults to 30 seconds.</param>
        /// <param name="timeout">
        /// Maximum time to wait before giving up. Leave unset to wait indefinitely (bounded only by
        /// <paramref name="cancellationToken"/> and the job's own 48-hour auto-expiry).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<BatchJob> WaitUntilCompleteAsync(
            string name,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a completed job's results. Works for both inline (already present on
        /// <paramref name="job"/>) and file-based (downloads and parses the JSONL output via
        /// <see cref="IFileService.DownloadFileAsync"/>) destinations. Throws
        /// <see cref="GeminiException"/> if the job has no output yet (i.e. hasn't reached a
        /// state where results exist) — call <see cref="WaitUntilCompleteAsync"/> or check
        /// <see cref="BatchJob.State"/> first.
        /// </summary>
        Task<IReadOnlyList<InlinedBatchResponse>> GetResultsAsync(BatchJob job, CancellationToken cancellationToken = default);
    }
}
