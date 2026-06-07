using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Unified service interface for all Gemini content generation operations
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// Generates content based on text input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateAsync(
            string prompt, 
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content based on text and image input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="image">The image data</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> GenerateWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates content based on chat history
        /// </summary>
        /// <param name="messages">Array of chat messages</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<GenerateContentResponse> ChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a text prompt, yielding each response chunk as it arrives.
        /// Iterate with <c>await foreach</c>; use <c>chunk.Text()</c> for the text of each chunk.
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="options">Optional generation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<GenerateContentResponse> StreamAsync(
            string prompt,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a text + image prompt, yielding each response chunk.
        /// </summary>
        IAsyncEnumerable<GenerateContentResponse> StreamWithImageAsync(
            string prompt,
            FileObject image,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams content generation for a chat history, yielding each response chunk.
        /// </summary>
        IAsyncEnumerable<GenerateContentResponse> StreamChatAsync(
            MessageObject[] messages,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience overload: streams a text prompt and invokes <paramref name="handleResponse"/>
        /// with the text of each chunk. Prefer the <see cref="IAsyncEnumerable{T}"/> overload for
        /// access to finish reason, usage, and safety data.
        /// </summary>
        Task StreamAsync(
            string prompt,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Convenience callback overload of <see cref="StreamWithImageAsync(string, FileObject, GeminiRequestOptions?, CancellationToken)"/>.</summary>
        Task StreamWithImageAsync(
            string prompt,
            FileObject image,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>Convenience callback overload of <see cref="StreamChatAsync(MessageObject[], GeminiRequestOptions?, CancellationToken)"/>.</summary>
        Task StreamChatAsync(
            MessageObject[] messages,
            Action<string> handleResponse,
            GeminiRequestOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensAsync(
            string prompt,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for text and image input
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="image">The image data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensWithImageAsync(
            string prompt,
            FileObject image,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for chat messages
        /// </summary>
        /// <param name="messages">Array of chat messages</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<CountTokensResponse> CountTokensChatAsync(
            MessageObject[] messages,
            CancellationToken cancellationToken = default);
    }
}