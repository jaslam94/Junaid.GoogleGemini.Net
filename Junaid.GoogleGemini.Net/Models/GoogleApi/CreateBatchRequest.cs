using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Body for <c>POST models/{model}:batchGenerateContent</c>. Wraps its payload in a top-level
/// <c>"batch"</c> property and deliberately has <b>no model field</b>. The model is expressed only in
/// the URL, matching every other endpoint in this library (<see cref="GenerateContentRequest"/> has no
/// model field either, for the same reason). See <c>PLAN-batch-api.md</c> §2.3 for the evidence this
/// was based on (the guide's own cURL example body has no model field at all).
/// </summary>
public class CreateBatchRequest
{
    /// <summary>The job's create-time payload.</summary>
    [JsonPropertyName("batch")]
    public BatchCreatePayload Batch { get; set; } = new();
}

/// <summary>The actual create-time fields, nested under <see cref="CreateBatchRequest.Batch"/>.</summary>
public class BatchCreatePayload
{
    /// <summary>Optional human-readable display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>Where the requests come from: inline, or a previously uploaded JSONL file.</summary>
    [JsonPropertyName("inputConfig")]
    public BatchJobSource InputConfig { get; set; } = new();
}
