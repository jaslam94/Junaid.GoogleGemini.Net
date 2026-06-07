using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Cached content: a reusable, pre-tokenized payload (large context, system instruction, tools) that
/// later requests reference by <see cref="Name"/> to save tokens and latency.
/// </summary>
public class CachedContent
{
    /// <summary>Resource name, e.g. <c>cachedContents/abc123</c> (assigned by the server).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The model this cache is bound to, e.g. <c>models/gemini-2.5-flash</c>.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Optional display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>The cached content.</summary>
    [JsonPropertyName("contents")]
    public List<Content>? Contents { get; set; }

    /// <summary>A cached system instruction.</summary>
    [JsonPropertyName("systemInstruction")]
    public Content? SystemInstruction { get; set; }

    /// <summary>Cached tools.</summary>
    [JsonPropertyName("tools")]
    public List<Tool>? Tools { get; set; }

    /// <summary>Creation timestamp (RFC3339).</summary>
    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }

    /// <summary>Last update timestamp (RFC3339).</summary>
    [JsonPropertyName("updateTime")]
    public string? UpdateTime { get; set; }

    /// <summary>Absolute expiry time (RFC3339). Mutually exclusive with <see cref="Ttl"/>.</summary>
    [JsonPropertyName("expireTime")]
    public string? ExpireTime { get; set; }

    /// <summary>Relative time-to-live, e.g. <c>"300s"</c>. Mutually exclusive with <see cref="ExpireTime"/>.</summary>
    [JsonPropertyName("ttl")]
    public string? Ttl { get; set; }

    /// <summary>Usage details (cached token count).</summary>
    [JsonPropertyName("usageMetadata")]
    public CachedContentUsageMetadata? UsageMetadata { get; set; }
}

/// <summary>Usage details for cached content.</summary>
public class CachedContentUsageMetadata
{
    /// <summary>Total tokens held in the cache.</summary>
    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}

/// <summary>Response for listing cached content.</summary>
public class CachedContentList
{
    /// <summary>The cached-content entries.</summary>
    [JsonPropertyName("cachedContents")]
    public CachedContent[]? CachedContents { get; set; }

    /// <summary>Token for the next page, if any.</summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
