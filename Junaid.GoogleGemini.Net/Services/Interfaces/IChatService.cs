using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for chat-based interactions using Gemini API
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Generates a response based on a chat conversation history
        /// </summary>
        /// <param name="chat">Array of messages representing the chat history</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The generated content response</returns>
        /// <exception cref="ArgumentException">Thrown when chat messages are invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown when content fails safety checks</exception>
        Task<GenerateContentResponse> GenereateContentAsync(
            MessageObject[] chat,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams a response based on a chat conversation history
        /// </summary>
        /// <param name="chat">Array of messages representing the chat history</param>
        /// <param name="handleStreamResponse">Callback to handle each chunk of streamed content</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <exception cref="ArgumentException">Thrown when chat messages are invalid</exception>
        /// <exception cref="ArgumentNullException">Thrown when handleStreamResponse is null</exception>
        Task StreamGenereateContentAsync(
            MessageObject[] chat,
            Action<string> handleStreamResponse,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts tokens for a chat conversation
        /// </summary>
        /// <param name="chat">Array of messages representing the chat history</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The token count response</returns>
        /// <exception cref="ArgumentException">Thrown when chat messages are invalid</exception>
        Task<CountTokensResponse> CountTokensAsync(
            MessageObject[] chat,
            CancellationToken cancellationToken = default);
    }
}