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
        /// <remarks>
        /// <b>Confirmed live: this field has no effect on <c>gemini-embedding-2</c>.</b> An identical
        /// request sent with and without <c>TaskType</c> set returned byte-for-byte identical
        /// embeddings; the API accepts the field (no error) but silently ignores it, matching
        /// Google's own docs ("you cannot use the task_type field for gemini-embedding-2"; include
        /// the task as an instruction in the prompt text instead). Older embedding models
        /// (<c>gemini-embedding-001</c>, <c>text-embedding-004</c>) are not affected by this; this is
        /// specific to <c>gemini-embedding-2</c>. See <c>PLAN-embeddings-multimodal.md</c> §2.
        /// <see cref="Junaid.GoogleGemini.Net.Services.EmbeddingService"/> logs a one-time warning when
        /// this is set together with that model.
        /// </remarks>
        public string? TaskType { get; set; }

        /// <summary>Optional document title (only meaningful with the RETRIEVAL_DOCUMENT task type).</summary>
        public string? Title { get; set; }

        /// <summary>Reduced output dimension; omit for the model's default.</summary>
        public int? OutputDimensionality { get; set; }
    }
}
