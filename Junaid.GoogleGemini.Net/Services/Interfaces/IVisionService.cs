using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for vision-related operations using Gemini API
    /// </summary>
    public interface IVisionService
    {
        /// <summary>
        /// Generates content based on text and image input
        /// </summary>
        /// <param name="text">The text prompt to accompany the image</param>
        /// <param name="fileObject">The image file object containing the image data</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The generated content response</returns>
        /// <exception cref="ArgumentException">Thrown when text or file inputs are invalid</exception>
        /// <exception cref="ArgumentNullException">Thrown when fileObject is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when content fails safety checks</exception>
        Task<GenerateContentResponse> GenereateContentAsync(
            string text,
            FileObject fileObject,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams generated content based on text and image input
        /// </summary>
        /// <param name="text">The text prompt to accompany the image</param>
        /// <param name="fileObject">The image file object containing the image data</param>
        /// <param name="handleStreamResponse">Callback to handle each chunk of streamed content</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <exception cref="ArgumentException">Thrown when text or file inputs are invalid</exception>
        /// <exception cref="ArgumentNullException">Thrown when fileObject or handleStreamResponse is null</exception>
        Task StreamGenereateContentAsync(
            string text,
            FileObject fileObject,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text and image input
        /// </summary>
        /// <param name="text">The text prompt to accompany the image</param>
        /// <param name="fileObject">The image file object containing the image data</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The token count response</returns>
        /// <exception cref="ArgumentException">Thrown when text or file inputs are invalid</exception>
        /// <exception cref="ArgumentNullException">Thrown when fileObject is null</exception>
        Task<CountTokensResponse> CountTokensAsync(
            string text,
            FileObject fileObject,
            CancellationToken cancellationToken = default);
    }
}