using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;

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
        /// <param name="model">The embedding model to use (e.g., "gemini-embedding-001")</param>
        /// <param name="text">The text to generate an embedding for</param>
        /// <param name="options">Optional embedding settings (task type, dimensionality, title)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The embedding response containing vector values</returns>
        /// <exception cref="ArgumentException">Thrown when model name or text is invalid</exception>
        Task<EmbedContentResponse> EmbedContentAsync(
            string model,
            string text,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates embeddings for multiple text inputs in a single batch
        /// </summary>
        /// <param name="model">The embedding model to use (e.g., "gemini-embedding-001")</param>
        /// <param name="texts">Array of texts to generate embeddings for</param>
        /// <param name="options">Optional embedding settings (task type, dimensionality, title)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The batch embedding response containing multiple embeddings</returns>
        /// <exception cref="ArgumentException">Thrown when model name is invalid, texts array is empty, or batch size exceeds limit</exception>
        Task<BatchEmbedContentResponse> BatchEmbedContentAsync(
            string model,
            string[] texts,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an embedding for inline media (image, audio, video, or PDF bytes), optionally
        /// combined with a text instruction, using a multimodal-capable embedding model (e.g.
        /// <c>gemini-embedding-2</c>; older models like <c>gemini-embedding-001</c> are text-only and
        /// will reject non-text parts). See <c>PLAN-embeddings-multimodal.md</c> and
        /// <c>docs/articles/multimodal-embeddings.md</c>.
        /// </summary>
        /// <param name="model">The embedding model to use (e.g. "gemini-embedding-2").</param>
        /// <param name="mediaBytes">The raw media bytes.</param>
        /// <param name="mimeType">The media's MIME type, e.g. "image/jpeg", "audio/wav".</param>
        /// <param name="text">Optional text instruction/content, combined with the media into one
        /// aggregated embedding (confirmed live: two parts in one request produce one embedding, not two).</param>
        /// <param name="options">Optional embedding settings. <see cref="EmbeddingOptions.TaskType"/>
        /// has no effect on <c>gemini-embedding-2</c>; see that property's own doc comment.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<EmbedContentResponse> EmbedContentAsync(
            string model,
            byte[] mediaBytes,
            string mimeType,
            string? text = null,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates an embedding for media already uploaded via
        /// <see cref="IFileService.UploadFileAsync"/>, optionally combined with a text instruction.
        /// Prefer this over the inline-bytes overload for large media.
        /// </summary>
        /// <param name="model">The embedding model to use (e.g. "gemini-embedding-2").</param>
        /// <param name="fileUri">The uploaded file's URI (<see cref="FileResource.Uri"/>).</param>
        /// <param name="mimeType">The media's MIME type, e.g. "image/jpeg", "audio/wav".</param>
        /// <param name="text">Optional text instruction/content, combined with the media into one aggregated embedding.</param>
        /// <param name="options">Optional embedding settings.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<EmbedContentResponse> EmbedFileAsync(
            string model,
            string fileUri,
            string mimeType,
            string? text = null,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates embeddings for multiple inputs in one batch call, each of which can mix text and
        /// media (unlike the text-only <see cref="BatchEmbedContentAsync(string, string[], EmbeddingOptions?, CancellationToken)"/>
        /// overload). Confirmed live: the batch endpoint already returns one separate embedding per
        /// input, in order, whether or not each input carries media.
        /// </summary>
        /// <param name="model">The embedding model to use (e.g. "gemini-embedding-2").</param>
        /// <param name="inputs">The inputs to embed, one embedding returned per entry, in order.</param>
        /// <param name="options">Optional embedding settings, applied to every input in the batch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<BatchEmbedContentResponse> BatchEmbedContentAsync(
            string model,
            IReadOnlyList<EmbeddingInput> inputs,
            EmbeddingOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}