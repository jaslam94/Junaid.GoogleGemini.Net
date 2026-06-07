using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Request wrapper for the batch embedding endpoint.</summary>
public class BatchEmbedContentRequest
{
    /// <summary>The individual embedding requests to process in one call.</summary>
    [JsonPropertyName("requests")]
    public EmbedContentRequest[] Requests { get; set; } = [];
}

/// <summary>A single embedding request (used inside a batch).</summary>
public class EmbedContentRequest
{
    /// <summary>The fully-qualified model name, e.g. <c>models/gemini-embedding-001</c>.</summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>The content to embed.</summary>
    [JsonPropertyName("content")]
    public Content Content { get; set; } = new();

    /// <summary>Optional task type the embedding is optimized for (see EmbeddingTaskTypes).</summary>
    [JsonPropertyName("taskType")]
    public string? TaskType { get; set; }

    /// <summary>Optional title (used with the RETRIEVAL_DOCUMENT task type).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Optional reduced output dimension.</summary>
    [JsonPropertyName("outputDimensionality")]
    public int? OutputDimensionality { get; set; }
}

/// <summary>
/// Request for single embedding generation (the simpler shape used by the direct
/// <c>embedContent</c> endpoint).
/// </summary>
public class SingleEmbedContentRequest
{
    /// <summary>The content to embed.</summary>
    [JsonPropertyName("content")]
    public Content Content { get; set; } = new();

    /// <summary>Optional task type the embedding is optimized for (see EmbeddingTaskTypes).</summary>
    [JsonPropertyName("taskType")]
    public string? TaskType { get; set; }

    /// <summary>Optional title (used with the RETRIEVAL_DOCUMENT task type).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Optional reduced output dimension.</summary>
    [JsonPropertyName("outputDimensionality")]
    public int? OutputDimensionality { get; set; }
}
