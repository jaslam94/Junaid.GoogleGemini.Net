using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Grounding information attached to a candidate when a grounding tool (e.g. Google Search) was used:
/// the queries issued, the sources consulted, and a renderable search-suggestion widget.
/// </summary>
public class GroundingMetadata
{
    /// <summary>The search queries the model issued.</summary>
    [JsonPropertyName("webSearchQueries")]
    public string[]? WebSearchQueries { get; set; }

    /// <summary>The source chunks used to ground the answer.</summary>
    [JsonPropertyName("groundingChunks")]
    public GroundingChunk[]? GroundingChunks { get; set; }

    /// <summary>A renderable "Search suggestions" entry point (HTML/CSS).</summary>
    [JsonPropertyName("searchEntryPoint")]
    public SearchEntryPoint? SearchEntryPoint { get; set; }
}

/// <summary>A single grounding source.</summary>
public class GroundingChunk
{
    /// <summary>Web source details.</summary>
    [JsonPropertyName("web")]
    public GroundingChunkWeb? Web { get; set; }
}

/// <summary>A web grounding source (URI + title).</summary>
public class GroundingChunkWeb
{
    /// <summary>The source URI.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    /// <summary>The source title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

/// <summary>The renderable search-suggestions widget for grounded responses.</summary>
public class SearchEntryPoint
{
    /// <summary>Rendered HTML/CSS content for the search-suggestions widget.</summary>
    [JsonPropertyName("renderedContent")]
    public string? RenderedContent { get; set; }
}
