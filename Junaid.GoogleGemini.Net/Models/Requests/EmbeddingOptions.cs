namespace Junaid.GoogleGemini.Net.Models.Requests
{
    /// <summary>
    /// Optional settings for embedding generation. <see cref="TaskType"/> tunes the embedding for a
    /// downstream use (retrieval, similarity, classification, …); <see cref="OutputDimensionality"/>
    /// truncates the vector to a smaller size.
    /// </summary>
    public class EmbeddingOptions
    {
        /// <summary>
        /// The task the embedding is optimized for (e.g. "RETRIEVAL_QUERY", "RETRIEVAL_DOCUMENT",
        /// "SEMANTIC_SIMILARITY"). See <c>GeminiConstants.EmbeddingTaskTypes</c>.
        /// </summary>
        public string? TaskType { get; set; }

        /// <summary>Optional document title (only meaningful with the RETRIEVAL_DOCUMENT task type).</summary>
        public string? Title { get; set; }

        /// <summary>Reduced output dimension; omit for the model's default.</summary>
        public int? OutputDimensionality { get; set; }
    }
}
