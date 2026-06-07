using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Response for the <c>batchEmbedContents</c> endpoint.</summary>
public class BatchEmbedContentResponse
{
    /// <summary>The generated embeddings, in the same order as the requests.</summary>
    [JsonPropertyName("embeddings")]
    public Embedding[]? Embeddings { get; set; }
}
