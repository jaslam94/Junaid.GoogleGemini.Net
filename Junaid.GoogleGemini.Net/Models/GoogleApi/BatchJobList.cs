using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Response for listing batch jobs.</summary>
/// <remarks>
/// <b>Wrapper field name, confirmed live (2026-08-15):</b> the real response wraps its array in a
/// field called <c>"operations"</c>, not <c>"batches"</c> as an earlier version of this type guessed by
/// analogy with <see cref="FileListResponse"/>/<see cref="CachedContentList"/>. This is consistent with
/// the rest of what live testing found for this feature: the whole Batch API surface is implemented as
/// instances of Google's generic long-running-operations pattern (see <see cref="BatchJob"/>'s
/// remarks), and <c>ListOperationsResponse</c>'s field is genuinely called <c>operations</c>. The C#
/// property here is still named <see cref="Batches"/>, not <c>Operations</c>, since that's the
/// meaningful name from this library's perspective; only the JSON mapping needed correcting.
/// </remarks>
public class BatchJobList
{
    /// <summary>The batch jobs.</summary>
    [JsonPropertyName("operations")]
    public BatchJob[]? Batches { get; set; }

    /// <summary>Token for the next page, if any.</summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
