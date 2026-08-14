using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Uploads and manages files via the Gemini Files API. Uploaded files can be referenced in a
    /// request through a <see cref="FileData"/> part (useful for large media you don't want to inline).
    /// </summary>
    public interface IFileService
    {
        /// <summary>Uploads a file (resumable protocol) and returns its metadata.</summary>
        /// <param name="content">The file bytes.</param>
        /// <param name="mimeType">The file's MIME type (e.g. "image/png", "video/mp4").</param>
        /// <param name="displayName">Optional display name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<FileResource> UploadFileAsync(
            byte[] content,
            string mimeType,
            string? displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>Gets metadata for a file by resource name (e.g. "files/abc" or "abc").</summary>
        Task<FileResource> GetFileAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>Lists uploaded files.</summary>
        Task<FileListResponse> ListFilesAsync(
            int? pageSize = null,
            string? pageToken = null,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes a file by resource name.</summary>
        Task DeleteFileAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a file's raw bytes by resource name. Used internally by the Batch API's
        /// GetResultsAsync for file-mode results, but works for any uploaded file.
        /// </summary>
        Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls until the file reaches <c>ACTIVE</c> state (required before using video/audio in a
        /// request). Throws if the file fails or the timeout elapses.
        /// </summary>
        Task<FileResource> WaitUntilActiveAsync(
            string name,
            TimeSpan? pollInterval = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }
}
