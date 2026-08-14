using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Response for listing batch jobs.</summary>
/// <remarks>
/// The <c>"batches"</c> wrapper field name follows this API's consistent pluralized-resource-name
/// convention for list responses (<c>files</c> for <see cref="FileListResponse"/>, <c>cachedContents</c>
/// for <see cref="CachedContentList"/>) — inferred by analogy, not directly confirmed by a fetched
/// research source the way most other fields in this feature were. Worth a quick sanity check the
/// first time <c>IBatchService.ListAsync</c> is exercised live.
/// </remarks>
public class BatchJobList
{
    /// <summary>The batch jobs.</summary>
    [JsonPropertyName("batches")]
    public BatchJob[]? Batches { get; set; }

    /// <summary>Token for the next page, if any.</summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
