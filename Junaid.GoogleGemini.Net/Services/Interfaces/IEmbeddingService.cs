using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Interface for generating embeddings using Gemini API
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates an embedding for a single text input
        /// </summary>
        /// <param name="model">The embedding model to use (e.g., "embedding-001")</param>
        /// <param name="text">The text to generate an embedding for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The embedding response containing vector values</returns>
        /// <exception cref="ArgumentException">Thrown when model name or text is invalid</exception>
        Task<EmbedContentResponse> EmbedContentAsync(
            string model,
            string text,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates embeddings for multiple text inputs in a single batch
        /// </summary>
        /// <param name="model">The embedding model to use (e.g., "embedding-001")</param>
        /// <param name="texts">Array of texts to generate embeddings for</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The batch embedding response containing multiple embeddings</returns>
        /// <exception cref="ArgumentException">Thrown when model name is invalid, texts array is empty, or batch size exceeds limit</exception>
        Task<BatchEmbedContentResponse> BatchEmbedContentAsync(
            string model,
            string[] texts,
            CancellationToken cancellationToken = default);
    }
}