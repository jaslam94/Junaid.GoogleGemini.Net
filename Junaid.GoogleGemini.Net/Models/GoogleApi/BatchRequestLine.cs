using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// One line of a Batch API JSONL input file (<b>file mode only</b>): <c>{"key": ..., "request": ...}</c>
/// per line, newline-delimited, not a single JSON document. Used by
/// <c>IBatchService.CreateFromRequestsFileAsync</c>, the convenience method that writes and uploads
/// this file for a caller so they never have to hand-write the JSONL protocol themselves.
/// </summary>
/// <remarks>
/// Deliberately a separate type from <see cref="InlinedBatchRequest"/> (the <b>inline</b>-mode
/// envelope) even though both simply wrap a <see cref="GenerateContentRequest"/> — the two modes use
/// different field names for the per-item correlation token (<c>key</c> here vs <c>metadata</c> for
/// inline), so they are not interchangeable wire shapes. See <c>PLAN-batch-api.md</c> §2.2/§2.3.
/// </remarks>
public class BatchRequestLine
{
    /// <summary>
    /// Caller-supplied correlation key, echoed back on the matching output line. Since file-mode output
    /// order is not documented as guaranteed to match input order, this is the only reliable way to
    /// match a result back to the request that produced it. Optional, but strongly recommended for
    /// anything beyond a single-request file.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>The actual generateContent request, same shape as any other call.</summary>
    [JsonPropertyName("request")]
    public GenerateContentRequest Request { get; set; } = new();
}
