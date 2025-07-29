using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for text-only operations using Gemini API
    /// </summary>
    public interface ITextService
    {
        /// <summary>
        /// Generates content based on a text prompt
        /// </summary>
        /// <param name="text">The text prompt to generate content from</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The generated content response</returns>
        /// <exception cref="ArgumentException">Thrown when text is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when content fails safety checks</exception>
        Task<GenerateContentResponse> GenereateContentAsync(
            string text,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams generated content based on a text prompt
        /// </summary>
        /// <param name="text">The text prompt to generate content from</param>
        /// <param name="handleStreamResponse">Callback to handle each chunk of streamed content</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <exception cref="ArgumentException">Thrown when text is null or empty</exception>
        /// <exception cref="ArgumentNullException">Thrown when handleStreamResponse is null</exception>
        Task StreamGenereateContentAsync(
            string text,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts the number of tokens in the given text
        /// </summary>
        /// <param name="text">The text to count tokens for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The token count response</returns>
        /// <exception cref="ArgumentException">Thrown when text is null or empty</exception>
        Task<CountTokensResponse> CountTokensAsync(
            string text,
            CancellationToken cancellationToken = default);
    }
}