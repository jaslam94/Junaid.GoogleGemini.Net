using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Response for the <c>embedContent</c> endpoint.</summary>
public class EmbedContentResponse
{
    /// <summary>The generated embedding.</summary>
    [JsonPropertyName("embedding")]
    public Embedding? Embedding { get; set; }
}

/// <summary>A single embedding vector.</summary>
public class Embedding
{
    /// <summary>The embedding values (the vector).</summary>
    [JsonPropertyName("values")]
    public float[]? Values { get; set; }
}
