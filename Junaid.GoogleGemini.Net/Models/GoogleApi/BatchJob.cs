using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A Batch API job (<c>batches/{id}</c>): an asynchronous, discounted-rate <c>generateContent</c> run
/// created from either inline requests or an uploaded JSONL file. See
/// <c>docs/articles/batch-api.md</c> for the full picture (submission modes, polling, result
/// retrieval, and the library's known limitations around this feature).
/// </summary>
/// <remarks>
/// Deliberately named <c>BatchJob</c>, not <c>Batch</c> — see <c>PLAN-batch-api.md</c> §4.1:
/// <see cref="BatchEmbedContentResponse"/> already exists for something unrelated (the synchronous
/// multi-embedding call), and a bare "Batch*" name would be an easy thing to confuse it with.
/// </remarks>
public class BatchJob
{
    /// <summary>Resource name, e.g. <c>batches/123456789</c> (assigned by the server).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional human-readable display name, set at creation.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// The model the job runs against, e.g. <c>models/gemini-3.6-flash</c>. Only present when reading
    /// a job back (Get/List) — the create request does not send this as a body field; the model is
    /// expressed in the create call's URL instead (see <c>PLAN-batch-api.md</c> §2.3).
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Where the job's input requests came from (inline or an uploaded file).</summary>
    [JsonPropertyName("inputConfig")]
    public BatchJobSource? InputConfig { get; set; }

    /// <summary>
    /// Where the job's results ended up, once available. Null until the job has made progress.
    /// </summary>
    [JsonPropertyName("output")]
    public BatchJobDestination? Output { get; set; }

    /// <summary>Creation timestamp (RFC 3339).</summary>
    [JsonPropertyName("createTime")]
    public string? CreateTime { get; set; }

    /// <summary>Last update timestamp (RFC 3339).</summary>
    [JsonPropertyName("updateTime")]
    public string? UpdateTime { get; set; }

    /// <summary>Completion timestamp (RFC 3339); set once the job reaches a terminal state.</summary>
    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }

    /// <summary>Per-request success/failure counts, populated as the job progresses.</summary>
    [JsonPropertyName("batchStats")]
    public BatchStats? BatchStats { get; set; }

    /// <summary>
    /// Optional creation-time priority hint. Documented by Google but not surfaced by any method on
    /// <c>IBatchService</c> in this release — see <c>PLAN-batch-api.md</c> §3/§9 for why (out of scope,
    /// not because it's unsupported on the wire).
    /// </summary>
    [JsonPropertyName("priority")]
    public long? Priority { get; set; }

    /// <summary>
    /// The job's current status. Modeled as a plain string (not a C# enum) deliberately — Google's own
    /// docs disagree with themselves on the exact prefix (<c>JOB_STATE_*</c> vs <c>BATCH_STATE_*</c>);
    /// see <c>PLAN-batch-api.md</c> §2.4. Compare via an ordinal-insensitive suffix check (does the
    /// value end in <c>"SUCCEEDED"</c>, <c>"FAILED"</c>, <c>"CANCELLED"</c>, or <c>"EXPIRED"</c>?)
    /// rather than an exact prefixed literal, so this keeps working whichever prefix the live API
    /// actually returns. <c>BatchService.IsTerminalState</c> implements exactly that check.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Job-level error, if the job itself failed outright (distinct from per-request errors).</summary>
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}

/// <summary>
/// The <c>inputConfig</c> oneof: either a reference to an uploaded JSONL file, or a list of inline
/// requests. Exactly one of <see cref="FileName"/> / <see cref="Requests"/> is set.
/// </summary>
public class BatchJobSource
{
    /// <summary>File-mode: the uploaded JSONL file's resource name (e.g. <c>files/abc123</c>).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    /// <summary>Inline-mode: the requests embedded directly in the create call.</summary>
    [JsonPropertyName("requests")]
    public InlinedBatchRequestList? Requests { get; set; }
}

/// <summary>Wrapper around the inline request list (<c>{ "requests": [ { "request": ... }, ... ] }</c>).</summary>
public class InlinedBatchRequestList
{
    /// <summary>The individual inline requests.</summary>
    [JsonPropertyName("requests")]
    public List<InlinedBatchRequest>? Requests { get; set; }
}

/// <summary>
/// One request in <b>inline</b> mode. Distinct from the JSONL file-mode line shape
/// (<see cref="BatchRequestLine"/>) — inline mode's per-item envelope field is
/// <c>metadata</c>, not <c>key</c>. Don't conflate the two; see <c>PLAN-batch-api.md</c> §2.3.
/// </summary>
public class InlinedBatchRequest
{
    /// <summary>The actual generateContent request, same shape as any other call.</summary>
    [JsonPropertyName("request")]
    public GenerateContentRequest Request { get; set; } = new();

    /// <summary>Optional caller-supplied metadata, echoed back on the matching response.</summary>
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}

/// <summary>
/// The <c>output</c>/<c>dest</c> oneof: either a reference to a results file, or the results embedded
/// directly in the job resource. Exactly one of <see cref="FileName"/> / <see cref="InlinedResponses"/>
/// is set, once the job has results.
/// </summary>
/// <remarks>
/// <b>Field-name uncertainty:</b> the field for the results file is modeled here as <c>fileName</c>,
/// which is what the guide's worked example and the Python SDK's own code sample both show. Google's
/// REST reference page says <c>responsesFile</c> instead — but that same page independently produced a
/// wrong answer elsewhere in this research (the <see cref="BatchJob.State"/> prefix, see
/// <c>PLAN-batch-api.md</c> §2.4), so it's the lower-confidence source here too. This has not yet been
/// confirmed against a live file-mode job's actual response — see <c>PLAN-batch-api.md</c> §7's
/// manual-verification note. If a live response turns out to use <c>responsesFile</c> instead, update
/// this property's <see cref="JsonPropertyNameAttribute"/> (case-insensitive matching does not bridge
/// the two, since they differ by more than case).
/// </remarks>
public class BatchJobDestination
{
    /// <summary>File-mode: the results file's resource name (e.g. <c>files/xyz789</c>). See remarks.</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    /// <summary>Inline-mode: the results embedded directly in the job resource.</summary>
    [JsonPropertyName("inlinedResponses")]
    public InlinedBatchResponseList? InlinedResponses { get; set; }
}

/// <summary>Wrapper around the inline response list (<c>{ "inlinedResponses": [ { "response": ... }, ... ] }</c>).</summary>
public class InlinedBatchResponseList
{
    /// <summary>The individual inline responses.</summary>
    [JsonPropertyName("inlinedResponses")]
    public List<InlinedBatchResponse>? InlinedResponses { get; set; }
}

/// <summary>
/// One result, whether read from <see cref="BatchJobDestination.InlinedResponses"/> or parsed from a
/// JSONL output file line. A oneof between <see cref="Response"/> (success) and <see cref="Error"/>
/// (this specific request failed; the job as a whole can still succeed).
/// </summary>
public class InlinedBatchResponse
{
    /// <summary>Echoes back the caller-supplied metadata (inline mode) or key (file mode), if any.</summary>
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }

    /// <summary>Echoes back the caller-supplied key (file/JSONL mode only).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>The successful response, if this request succeeded.</summary>
    [JsonPropertyName("response")]
    public GenerateContentResponse? Response { get; set; }

    /// <summary>The error, if this specific request failed.</summary>
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }
}

/// <summary>Per-request progress/outcome counts for a batch job.</summary>
public class BatchStats
{
    /// <summary>Total number of requests in the job.</summary>
    [JsonPropertyName("requestCount")]
    public long? RequestCount { get; set; }

    /// <summary>Requests that completed successfully so far.</summary>
    [JsonPropertyName("successfulRequestCount")]
    public long? SuccessfulRequestCount { get; set; }

    /// <summary>Requests that failed so far.</summary>
    [JsonPropertyName("failedRequestCount")]
    public long? FailedRequestCount { get; set; }

    /// <summary>Requests still pending.</summary>
    [JsonPropertyName("pendingRequestCount")]
    public long? PendingRequestCount { get; set; }
}
