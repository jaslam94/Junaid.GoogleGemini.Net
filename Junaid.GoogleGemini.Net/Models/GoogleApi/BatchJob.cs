using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A Batch API job (<c>batches/{id}</c>): an asynchronous, discounted-rate <c>generateContent</c> run
/// created from either inline requests or an uploaded JSONL file. See
/// <c>docs/articles/batch-api.md</c> for the full picture (submission modes, polling, result
/// retrieval, and the library's known limitations around this feature).
/// </summary>
/// <remarks>
/// <b>Wire shape, confirmed live (2026-08-15) against the real API</b> — this took a real key to get
/// right and corrects an earlier, docs-only version of this type that had the wrong shape entirely.
/// Create/Get/List do not return the batch fields (state, batchStats, output, ...) directly at the
/// JSON root. They return a Google long-running-<c>Operation</c> envelope
/// (<c>{ name, metadata, done, error | response }</c>), with the actual batch resource nested under
/// <c>metadata</c>. This wasn't documented anywhere in Google's guide, REST reference, or cookbook
/// research for this feature; it was only visible by making real calls. See <see cref="Metadata"/> and
/// the passthrough properties below for how this is handled without changing the public shape callers
/// already write code against.
/// </remarks>
public class BatchJob
{
    /// <summary>Resource name, e.g. <c>batches/123456789</c> (assigned by the server).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The actual batch resource fields, as nested by the API under the long-running-operation
    /// envelope's <c>metadata</c> property. Most callers won't need to touch this directly; the
    /// passthrough properties below (<see cref="DisplayName"/>, <see cref="State"/>, etc.) read from
    /// here so existing code keeps working against the flat shape.
    /// </summary>
    [JsonPropertyName("metadata")]
    public BatchJobMetadata? Metadata { get; set; }

    /// <summary>
    /// Whether the underlying operation has finished (in any outcome: succeeded, failed, cancelled, or
    /// expired). Absent (and therefore <c>false</c>, proto3's default-value-omission behavior) while
    /// still pending/running - confirmed live, not merely assumed. Prefer
    /// <c>BatchService.IsTerminalState</c> on <see cref="State"/> for polling logic; this flag
    /// is provided for completeness since it's what the raw API actually exposes.
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }

    /// <summary>
    /// The operation-level error, present when <see cref="Done"/> and the job did not succeed
    /// (including a user-initiated cancellation - confirmed live: a cancelled job's <c>error</c> is
    /// populated too, with a generic message, not left null).
    /// </summary>
    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    /// <summary>
    /// The operation's result payload, present once <see cref="Done"/> and the job succeeded. Same
    /// shape as <see cref="BatchJobMetadata.Output"/> - confirmed live, Google duplicates the output
    /// here and under <c>metadata.output</c> once a job completes. <see cref="Output"/> below prefers
    /// this field (the canonical long-running-operation "result" location) and falls back to
    /// <see cref="BatchJobMetadata.Output"/> only if this is somehow absent.
    /// </summary>
    [JsonPropertyName("response")]
    public BatchJobDestination? Response { get; set; }

    /// <summary>Optional human-readable display name, set at creation.</summary>
    [JsonIgnore]
    public string? DisplayName => Metadata?.DisplayName;

    /// <summary>The model the job runs against, e.g. <c>models/gemini-3.6-flash</c>.</summary>
    [JsonIgnore]
    public string? Model => Metadata?.Model;

    /// <summary>Where the job's input requests came from (inline or an uploaded file).</summary>
    [JsonIgnore]
    public BatchJobSource? InputConfig => Metadata?.InputConfig;

    /// <summary>
    /// Where the job's results ended up, once available. Null until the job has made progress. Reads
    /// <see cref="Response"/> first, falling back to <see cref="BatchJobMetadata.Output"/> - see
    /// <see cref="Response"/>'s remarks for why there are two places this could come from.
    /// </summary>
    [JsonIgnore]
    public BatchJobDestination? Output => Response ?? Metadata?.Output;

    /// <summary>Creation timestamp (RFC 3339).</summary>
    [JsonIgnore]
    public string? CreateTime => Metadata?.CreateTime;

    /// <summary>Last update timestamp (RFC 3339).</summary>
    [JsonIgnore]
    public string? UpdateTime => Metadata?.UpdateTime;

    /// <summary>Completion timestamp (RFC 3339); set once the job reaches a terminal state.</summary>
    [JsonIgnore]
    public string? EndTime => Metadata?.EndTime;

    /// <summary>Per-request success/failure counts, populated as the job progresses.</summary>
    [JsonIgnore]
    public BatchStats? BatchStats => Metadata?.BatchStats;

    /// <summary>
    /// Optional creation-time priority hint. Documented by Google but not surfaced by any method on
    /// <c>IBatchService</c> in this release - see <c>PLAN-batch-api.md</c> §3/§9 for why (out of scope,
    /// not because it's unsupported on the wire).
    /// </summary>
    [JsonIgnore]
    public long? Priority => Metadata?.Priority;

    /// <summary>
    /// The job's current status. Modeled as a plain string (not a C# enum) even though the exact prefix
    /// is now confirmed (<c>BATCH_STATE_*</c> - see <see cref="Metadata"/>'s type remarks): a string
    /// still round-trips correctly if Google ever changes it again, and
    /// <c>BatchService.IsTerminalState</c>'s suffix match doesn't care about the prefix anyway.
    /// </summary>
    [JsonIgnore]
    public string? State => Metadata?.State;
}

/// <summary>
/// The actual <c>GenerateContentBatch</c> resource fields, as returned nested under a
/// long-running-operation envelope's <c>metadata</c> property (see <see cref="BatchJob"/>'s remarks).
/// </summary>
/// <remarks>
/// <b>State prefix, confirmed live (2026-08-15):</b> real responses use <c>BATCH_STATE_PENDING</c>,
/// <c>BATCH_STATE_RUNNING</c>, <c>BATCH_STATE_SUCCEEDED</c>, and <c>BATCH_STATE_CANCELLED</c> - i.e.
/// Google's REST reference page was right, and the guide's/cookbook's <c>JOB_STATE_*</c> examples
/// reflect the Python SDK's own naming, not the raw wire format. Still modeled as a plain string
/// (see <see cref="BatchJob.State"/>) rather than hardcoding this prefix into an enum, since a suffix
/// match is just as correct and doesn't risk breaking if Google changes it a third time.
/// </remarks>
public class BatchJobMetadata
{
    /// <summary>
    /// The model this job runs against, e.g. <c>models/gemini-3.6-flash</c>. Present on Get/List;
    /// confirmed absent from the create request body itself (see <c>CreateBatchRequest</c>'s remarks -
    /// the model is expressed only in the create call's URL).
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Optional human-readable display name, set at creation.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>Where the job's input requests came from (inline or an uploaded file).</summary>
    [JsonPropertyName("inputConfig")]
    public BatchJobSource? InputConfig { get; set; }

    /// <summary>
    /// Where the job's results ended up, once available. See <see cref="BatchJobDestination"/> - the
    /// same content is also duplicated at the envelope's top-level <c>response</c> field once the job
    /// completes (<see cref="BatchJob.Response"/>).
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
    /// Optional creation-time priority hint. Confirmed (by analogy with <see cref="BatchStats"/>'s
    /// fields, both being <c>int64</c> per Google's REST reference) to likely arrive as a JSON string
    /// rather than a JSON number - not itself observed live (no test job set a priority), but handled
    /// the same defensive way regardless.
    /// </summary>
    [JsonPropertyName("priority")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? Priority { get; set; }

    /// <summary>The job's current status. See this type's remarks for the confirmed state prefix.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    // Note: the raw response also duplicates "name" inside metadata (identical to the envelope's
    // top-level name in every observed case). Deliberately not modeled here - BatchJob.Name is the
    // canonical source, and an unmapped "name" inside this object is silently ignored by
    // System.Text.Json's default unmapped-member handling, same as any other unmodeled field.
}

/// <summary>
/// The <c>inputConfig</c> oneof: either a reference to an uploaded JSONL file, or a list of inline
/// requests. Exactly one of <see cref="FileName"/> / <see cref="Requests"/> is set. Confirmed live
/// (2026-08-15): <c>{"inputConfig": {"fileName": "files/..."}}</c> is exactly the shape the real API
/// accepts for file mode.
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
/// (<see cref="BatchRequestLine"/>) - inline mode's per-item envelope field is
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
/// The <c>output</c>/<c>response</c> oneof: either a reference to a results file, or the results
/// embedded directly. Exactly one of <see cref="ResponsesFile"/> / <see cref="InlinedResponses"/> is
/// set, once the job has results.
/// </summary>
/// <remarks>
/// <b>Field name, confirmed live (2026-08-15):</b> the results-file field is <c>responsesFile</c>, not
/// <c>fileName</c> - an earlier version of this type guessed <c>fileName</c> based on the guide's
/// worked example and the Python SDK's own sample code, both of which turned out to describe the SDK's
/// abstraction rather than the raw wire field name. Confirmed directly from a real completed file-mode
/// job's response: <c>{"output": {"responsesFile": "files/batch-..."}}</c>, duplicated identically at
/// the envelope's top-level <c>response.responsesFile</c>. Inline mode's shape
/// (<c>{"inlinedResponses": {"inlinedResponses": [...]}}</c>, the double-nesting is real, also
/// confirmed live) needed no correction.
/// </remarks>
public class BatchJobDestination
{
    /// <summary>File-mode: the results file's resource name (e.g. <c>files/xyz789</c>).</summary>
    [JsonPropertyName("responsesFile")]
    public string? ResponsesFile { get; set; }

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

/// <summary>
/// Per-request progress/outcome counts for a batch job. Confirmed live (2026-08-15): every count here
/// arrives as a JSON <b>string</b> (e.g. <c>"requestCount": "1"</c>), not a JSON number - the
/// protobuf-JSON convention for <c>int64</c> fields (avoids precision loss in JS number parsing).
/// <see cref="JsonNumberHandlingAttribute"/> below tolerates that; without it, deserializing a real
/// response into <c>long?</c>-typed properties would throw.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
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
