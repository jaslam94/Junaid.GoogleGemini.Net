# Plan: Batch API for Junaid.GoogleGemini.Net

Status: implemented and live-verified in `6.4.0` (PR #42) on 2026-08-15. We keep this file as a
historical record, with the same convention as `PLAN-cost-governance.md`.

Live-verification addendum (2026-08-15, after we finally used a real key): this plan took care to
avoid false certainty. It had a self-review pass and a source-reliability calibration. It left
§2.4 and §2.3 as unresolved facts and did not guess. That instinct was right, but it was not enough
on its own. Live testing resolved both flagged facts differently than this plan's working
assumptions. It also found a third, more serious gap. None of the three research sources (guide,
REST reference, cookbook) showed it as a central point. Create, Get, and List responses come wrapped
in a Google long-running-operation envelope (`{ name, metadata, done, error | response }`). They are
not the flat resource that this entire plan assumed in §2.3 and §4. The real batch fields live under
`metadata`. Against the real API, the originally shipped flat `BatchJob` would have deserialized
`State`, `BatchStats`, and `DisplayName` as permanently null. Then `WaitUntilCompleteAsync` would
have looped until timeout on every real job.

One of this plan's own early research fetches did mention an "Operation" shape. We left it out of
the final plan text because it looked like a side issue. It was the actual answer, and we had filed
it in the wrong place. The lesson for next time: when research shows a shape that does not obviously
fit the working model, do not file it away. Work out where it fits before you continue.

See the `6.4.0` entry in `ROADMAP.md` for the full list of what live testing corrected: the state
prefix, the results-file field name, the operation-envelope restructure, and the string-typed
numbers in `batchStats`. That entry also records how we verified each one: raw REST calls that are
independent of this library's own types, plus the actual library code, run against real jobs taken
to real completion.

Post-implementation addendum (before live verification, still accurate): `CancelAsync` ended up
simpler than §4.7 specified. The plan said to deserialize into the `EmptyBatchResponse` type. The
actual implementation discards the cancel response body entirely and decides success or failure
only from the HTTP status code. So we never added `EmptyBatchResponse` to the codebase. The reasoning
in §4.7 for why we cannot trust the response shape still holds. It turned out that we did not need
to model it at all, once the implementation stopped trying to read it.

A second, unplanned addition: `GetResultsAsync` throws, and does not return an empty list, when a
job's `Output` object exists but has neither inline responses nor a results file name. We found this
in a second-pass review after implementation. An empty list there would look the same as a
legitimately empty result set. It could silently hide the exact field-name risk that §2.3 flags.
Everything else below matches what shipped.

Audience: an AI coding agent (Cursor) that implements this directly against the current `master`
branch (after 6.3.1).

Author's confidence: we verified every file path, method signature, and integration point below by
reading the actual current source on 2026-08-15. We researched the shape of the Gemini Batch API
itself from Google's live docs on the same date (see §2). This plan has already been through one
self-review pass. That pass caught several wrong or unverifiable claims from the first draft: a
fabricated citation, a wire-format field-name error, and a self-contradiction about the type of the
state field. We corrected all of them below and marked each with the evidence that supports it.

Two material facts could not be resolved with full confidence from docs alone: the state-string
values in §2.4 and the destination-file field name in §2.3. You must confirm both against a real API
response before you finalize those details in code. Do not guess. §2.4 and §7 say exactly how to
resolve both cheaply, in the same live test.

A note on source reliability, learned during this research: we fetched the REST reference page
(`ai.google.dev/api/batch-mode`) twice, independently. Both times it produced at least one detail
that direct evidence from other sources contradicts. Those sources are the guide's own worked
examples and the actual printed API output in the official cookbook notebook. Treat any fact below
that comes only from that page, with no corroboration, as the weakest confidence tier. We call those
out individually below, and do not present them with the same confidence as corroborated facts.

---

## 1. Goal

Let a caller submit large volumes of `generateContent` requests asynchronously, at Google's
advertised 50% cost discount. The caller should not have to hand-roll the JSONL file protocol, the
polling loop, or the result parsing.

This is a new resource client (`IBatchService`). It follows the same layering that the codebase
already uses for Files (`IFileService`) and context caching (`ICachingService`): a thin, faithful 1:1
mapping onto the batch endpoints. We do not fold it into the `IGeminiService` facade. Batch jobs are
a different usage pattern (create, then poll, then fetch, and not request and response). So they get
their own service. This matches principle 1 in the guiding principles of `ROADMAP.md`.

## 2. What the Batch API actually is (researched 2026-08-15)

We cross-checked three independent sources: the official guide
(`https://ai.google.dev/gemini-api/docs/batch-api`), the REST reference
(`https://ai.google.dev/api/batch-mode`), and the official cookbook notebook
(`google-gemini/cookbook`, `quickstarts/Batch_mode.ipynb`). Where the sources disagreed, we call that
out explicitly and do not silently pick one.

### 2.1 Endpoints

| Operation | Method | Path |
|---|---|---|
| Create | `POST` | `models/{model}:batchGenerateContent` |
| Get | `GET` | `batches/{batchId}` |
| List | `GET` | `batches` |
| Cancel | `POST` | `batches/{batchId}:cancel` |
| Delete | `DELETE` | `batches/{batchId}` |

All paths are relative to the same versioned base address that `IGeminiClient` already targets. Do
not prefix them with `v1beta/` in code. Copy the `"cachedContents"` endpoint of `CachingService`
verbatim as the pattern, because the `HttpClient.BaseAddress` of `GeminiClient` already includes the
version segment.

Downloading a file-based result uses a different path. It is rooted at the host authority and not at
the versioned base address:

```
GET /download/v1beta/{fileName}:download?alt=media
```

This is the same shape that `FileService` already uses for uploads (`upload/v1beta/files`). That is
why it uses a separate root-relative `HttpClient` (`GeminiHttpClients.Files`). See §4.5. This plan
adds a `DownloadFileAsync` method to `IFileService` and not to `IBatchService`. Download is a Files
API capability that batch happens to depend on. It is also independently useful for any large file,
beyond batch output.

### 2.2 Submitting a batch: two mutually exclusive modes

1. Inline requests: a list of `GenerateContentRequest` objects embedded directly in the create call.
   Google's documented ceiling is under 20MB of total request size.
2. File input (JSONL): first upload a JSON Lines file through the existing Files API
   (`IFileService.UploadFileAsync`, MIME type `"jsonl"`), then reference it by name. The ceiling is
   2GB per file. This is the recommended path for anything non-trivial.

A JSONL input line has this shape (one `GenerateContentRequest` per line, optionally keyed):

```json
{"key": "request-1", "request": {"contents": [{"parts": [{"text": "..."}]}]}}
```

The caller supplies the `key`, and the API echoes it back on the matching output line. In file mode,
this is the only way to match an output line back to its input. Google does not document that output
order matches input order. `key` is a JSONL-only concept (see the correction in §2.3: the per-item
field in inline mode is `metadata`, not `key`. The two modes are not interchangeable here).

The gap this creates, closed in §4.2: the stated goal (§1) is that callers should not hand-roll the
JSONL protocol themselves. But suppose `IBatchService` offers only `CreateFromFileAsync(model,
fileName, ...)` for an already-uploaded file. A caller with an in-memory list of requests would still
have to hand-write JSONL lines and call `IFileService.UploadFileAsync` themselves. The goal says
this feature should spare them exactly that. §4.2 adds a convenience method that writes the JSONL and
uploads it internally. This way the library keeps the promise for both submission modes.

### 2.3 The `Batch` resource (get, list, and create response shape)

```
name              string   "batches/123456789"                  (output only)
displayName       string
model             string   "models/gemini-3.6-flash"             (echoed back on Get/List; see below)
inputConfig       object   oneof: fileName (string) | requests (InlinedRequest[])
output / dest     object   oneof: fileName (string) | inlinedResponses (InlinedResponse[])  (see note
createTime        string   RFC 3339
updateTime        string   RFC 3339
endTime           string   RFC 3339                              (set once terminal)
batchStats        object   requestCount, successfulRequestCount, failedRequestCount, pendingRequestCount
priority          int64    optional, create-time only (documented but not used by v1, see §3)
state             string   see §2.4
```

On `model` and the create request body: the guide's own cURL example for creating a file-based batch
job sends `{"batch": {"display_name": ..., "input_config": {"file_name": ...}}}`. The body has no
`model` or `display_model` field at all. The model appears only in the URL
(`models/{model}:batchGenerateContent`). This matches how every other endpoint in this codebase
builds request URLs (`GenerateContentRequest` has no `Model` field either, for the same reason). So
the body of `CreateBatchRequest` must not include a model field. `model` appears as a field only when
you read a `BatchJob` back through Get or List. The server fills it from the URL at creation. An
earlier draft of this plan built the create request with a `Model` property. That would be wrong.
Remove it from `CreateBatchRequest` entirely.

The create request body has a top-level `"batch"` wrapper. We confirmed this directly from both the
guide's cURL example and the method signature of the REST reference
(`{batch.model=models/*}:batchGenerateContent`). `CreateBatchRequest` must serialize as
`{ "batch": { "displayName": ..., "inputConfig": {...} } }` and not as a bare
`{ "displayName": ..., ... }`. This is easy to miss. It would silently produce a request that
Google's API rejects or ignores.

Unresolved field-name discrepancy, `fileName` or `responsesFile`, for the output and destination
file field: the guide's own worked JSON sketch (`"dest": {"fileName": "files/output123", ...}`) and
the actual code sample of the Python SDK (`batch_job.dest.file_name`, which makes sense only if the
underlying JSON field is `fileName`) both point to `fileName`. Only the REST reference page says
`responsesFile`. That is the source that we already found unreliable once, on the state enum (see
§2.4). Given its track record here, prefer `fileName` as the working assumption. But do not ship
this without confirming it against a real response. The same live test that resolves §2.4 (§7)
should assert which key is actually present in a real `output` or `dest` object once a job
completes. At minimum, log the raw JSON once during manual verification.

`InlinedRequest`: `{ request: GenerateContentRequest, metadata?: object }`. The documented wrapper
field name for inline mode is `metadata`, not `key`. `key` is a concept that exists only on JSONL
lines, for file mode (see §2.2). These are two different envelopes around the same `request`
payload. They are not the same field under two names. Do not reuse one type for both without
accounting for that. `InlinedBatchRequest` (§4.1) models the inline shape only.

`InlinedResponse`: `{ metadata?: object, response?: GenerateContentResponse, error?: Status }`. It
is a oneof between `response` and `error`, exactly like the per-line JSONL output (`{"response":
...}` or `{"error": {"code": ..., "message": ...}}`).

### 2.4 State enum: UNRESOLVED, resolve before you finalize

Two different naming schemes turned up, depending on the source:

- The user guide, the actual printed cell output of the cookbook notebook (`batch_job.state.name`
  from real API calls), and multiple corroborating forum threads all show: `JOB_STATE_PENDING`,
  `JOB_STATE_RUNNING`, `JOB_STATE_SUCCEEDED`, `JOB_STATE_FAILED`, `JOB_STATE_CANCELLED`,
  `JOB_STATE_EXPIRED`.
- The REST API reference page showed a `BATCH_STATE_*` prefix instead. We fetched it twice
  independently, and it showed the same both times.
- One old developer-forum thread (lower-numbered, probably from 2025) also used
  `BATCH_STATE_RUNNING`. This suggests that Google may have renamed the API from `BATCH_STATE_*` to
  `JOB_STATE_*` at some point, and that stale caches of the REST reference still show the old name.

Do not pick one from docs alone. Resolve it the way this codebase already resolved a similar
gap between docs and reality for `thoughtSignature` (see the live-verification precedent in
`PLAN-cost-governance.md`). The very first live test that you write for this feature (§7) must
assert on the actual string in `state` from a real `batches.get` response. That observed value is
what goes in the shipped enum or constants. If the API somehow accepts both prefixes (that is, it
tolerates case or aliases), prefer whichever the guide documents (`JOB_STATE_*`), because that is
the user-facing contract.

Model `state` as a `string` on the wire type, and not as a C# `enum`. Add `GeminiConstants` string
constants for the known values. This is exactly the existing pattern for `FileResource.State`
(`"ACTIVE"`, `"FAILED"`, and so on are plain strings, matched with
`string.Equals(..., StringComparison.OrdinalIgnoreCase)` in `FileService.WaitUntilActiveAsync`). It
avoids the naming ambiguity entirely. Whichever prefix the live API returns, a string-typed field
round-trips it correctly. Callers that compare ignoring case (as the `WaitUntilCompleteAsync` helper
below will) work either way.

### 2.5 Terminal states, timing, retention

- Terminal states: `SUCCEEDED`, `FAILED`, `CANCELLED`, and `EXPIRED`. Whatever the resolved prefix,
  these four suffixes are the same in every source.
- A job expires automatically (`EXPIRED`) if it is still `PENDING` or `RUNNING` after 48 hours.
- The target turnaround is "24 hours, often faster." It is not a hard SLA, and Google documents no
  minimum.
- Results (both inline and file-based) are retrievable for 6 weeks after completion, then deleted.
- Google explicitly documents job creation as not idempotent. If you call create twice with the same
  logical content, you get two separate jobs. There is no dedup or upsert by displayName.

### 2.6 Pricing

The discount is a flat 50% against the standard (interactive) rate. We confirmed that it holds
proportionally across every pricing tier that we checked. That includes the long-context tiers above
200k tokens for `gemini-2.5-pro` and `gemini-3.1-pro-preview`. The source is the live pricing page,
which we fetched on the same day as this research. Cost-governance integration is explicitly out of
scope for this plan. See the exclusions in §3 and see §9 for why, and for what a follow-up would
need.

### 2.7 Access requirements and limits

- The Batch API requires a paid-tier project. It is not available on the free tier.
- Batch jobs draw from a separate quota pool from interactive requests (their own limits for
  concurrent jobs, files, storage, and enqueued tokens). The existing `IRateLimiter` and
  `ICostGovernor` are both scoped to interactive calls. This feature correctly leaves them
  untouched (see §3).
- Documented ceiling: 100 concurrent batch jobs per project (as of the source that we checked).
  Treat this as information only. This library does not enforce it. The API rejects creates over the
  limit.
- The Batch API currently supports only `generateContent` (this plan's scope). The cookbook examples
  also show a separate `create_embeddings` batch surface, and image generation in batch (the same
  endpoint, with `responseModalities` in the per-request config). See §3 for what is in and out of
  scope.

## 3. Explicit scope

In scope (v1):
- `IBatchService` that covers create (inline and file input), get, list, cancel, and delete.
- Both submission modes for `generateContent`-shaped requests. This includes requests that
  themselves use system instructions, tools, and generation config (that is,
  `GenerateContentRequest`, the existing wire model). No new request-shaping logic is needed. Only a
  new envelope around it.
- A `WaitUntilCompleteAsync` polling helper. It mirrors the exact shape of
  `IFileService.WaitUntilActiveAsync` (`pollInterval`, `timeout`, cancellation).
- Result retrieval for both inline outcomes (`dest.inlinedResponses`, already in the Get response)
  and file-based outcomes. The file-based case uses the destination file field (name to be decided
  per §2.3). It needs the new `IFileService.DownloadFileAsync` plus JSONL parsing.
- A new `IFileService.DownloadFileAsync(string name, CancellationToken ct)` method (it returns raw
  `byte[]` or a `string`, see §4.5). Retrieving file-based batch output requires it, and no existing
  method covers "download arbitrary file bytes by name."
- A JSONL-writing convenience method (`CreateFromRequestsFileAsync` in §4.2). It builds the JSONL
  payload from an in-memory list of requests and uploads it through `IFileService`. File-mode
  submission then does not require the caller to hand-write the JSONL protocol either. We added it
  in this review pass, and it is not optional polish. See the gap note in §2.2 for why.

Out of scope for this change (do not implement):
- Cost governance integration. `ICostGovernor` prices interactive calls in real time from the
  `UsageMetadata` of a single response. Batch responses carry `usageMetadata` too (confirmed in the
  cookbook's example JSONL output). So the cost of a batch job could be computed after the fact. But
  the cost would be at the batch (discounted) rate, and today's `ModelPricing` has no concept of
  that. It is a flat rate per model. It has no rate-plus-batch-multiplier. Bolting this on half
  finished would be worse than not touching it. Note it as a real, sized follow-up in the ROADMAP
  entry that this plan produces (§10). Do not attempt it here.
- Batch embeddings (`create_embeddings`). They have a different request and response shape and a
  different model (`gemini-embedding-*`). Also, the `EmbedContentResponse` of this library already
  has no usage or token field (the same gap that excluded embeddings from cost governance). This
  needs separate design work.
- Webhooks (`client.webhooks.create(...)` as an alternative to polling). They appear in the cookbook
  but not in the primary guide. They look newer and less battle-tested. This library also has no
  existing webhook-receiving story to build on. It is a client library, and not a hosted service.
  Polling through `WaitUntilCompleteAsync` is enough for v1.
- Rate limiting integration. Batch draws from a separate quota pool (§2.7). Routing batch calls
  through `IRateLimiter` (tuned for interactive RPM) would be wrong. Batch calls bypass the rate
  limiter entirely, as `IFileService` already does today.
- Client-side enforcement of the 100-concurrent-job, 20MB, and 2GB limits. Let the API reject
  over-limit requests with its normal error response. This library surfaces that through the
  existing `GeminiApiException` path, like every other endpoint. Client-side pre-validation of size
  limits is nice to have and not required for v1. Flag it as a possible follow-up. Do not build it
  now.
- A sample endpoint in `AspNetCoreSample`. It is worth doing once the feature ships and is verified
  live. It is a separate, smaller follow-up. The reasoning is the same as for the cost-governance
  sample work, which we did as its own pass after 6.2.0 shipped and did not bundle into it.

## 4. Architecture

### 4.1 New files

```
Junaid.GoogleGemini.Net/Services/Interfaces/IBatchService.cs
Junaid.GoogleGemini.Net/Services/BatchService.cs
Junaid.GoogleGemini.Net/Models/GoogleApi/BatchJob.cs           (Batch resource + nested types)
Junaid.GoogleGemini.Net/Models/GoogleApi/BatchJobList.cs       (list response)
Junaid.GoogleGemini.Net/Models/Requests/CreateBatchRequest.cs  (create request envelope)
```

Naming note. Read this before you name anything. `Models/GoogleApi/BatchEmbedContentResponse.cs`
already exists. It belongs to the unrelated synchronous multi-embedding call (the batch variant of
`EmbedContentAsync`, a single request and response pair, with nothing to do with this feature). Do
not name any new type `Batch...ContentRequest` or `Response`. The meaning will collide, even if the
compiler symbol does not. Suggested names:
- `BatchJob`: the resource itself. This avoids the bare `Batch` collision risk, including with the
  vocabulary of `BudgetOptions`, which sits next to `GeminiOptions.Budget`.
- `BatchJobSource`: the `inputConfig` oneof.
- `BatchJobDestination`: the `output` or `dest` oneof.
- `InlinedBatchRequest`: the per-item envelope for inline mode (`Request` plus `Metadata`, no
  `Key`).
- `InlinedBatchResponse`.
- `BatchStats`.
- `BatchJobList`.
- `BatchRequestLine`: a small type that only `CreateFromRequestsFileAsync` and the JSONL writing
  use (`Key` plus `Request`). Do not merge this with `InlinedBatchRequest`. They serialize to
  different wire shapes, per the correction in §2.3.

`CreateBatchRequest` (the body of the create call) wraps its payload in a top-level `"batch"`
property. It has no `Model` property at all. See the correction in §2.3. Its shape is effectively
`{ Batch: { DisplayName, InputConfig } }`.

### 4.2 `IBatchService`

```csharp
public interface IBatchService
{
    /// <summary>Creates a batch job from inline requests (kept under ~20MB total).</summary>
    Task<BatchJob> CreateAsync(
        string model,
        IReadOnlyList<InlinedBatchRequest> requests,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a batch job from a previously uploaded JSONL file (see IFileService.UploadFileAsync).</summary>
    Task<BatchJob> CreateFromFileAsync(
        string model,
        string fileName,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience wrapper: writes <paramref name="requests"/> as JSONL, uploads it via
    /// IFileService.UploadFileAsync(..., mimeType: "jsonl"), then calls CreateFromFileAsync. Exists so
    /// a caller with an in-memory list never has to hand-write the JSONL protocol themselves (see §2.2).
    /// This is the same reason CreateAsync exists for the inline path.
    /// </summary>
    Task<BatchJob> CreateFromRequestsFileAsync(
        string model,
        IEnumerable<BatchRequestLine> requests,
        string? displayName = null,
        string? fileDisplayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a batch job's current status by resource name ("batches/123" or "123").</summary>
    Task<BatchJob> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Lists batch jobs.</summary>
    Task<BatchJobList> ListAsync(int? pageSize = null, string? pageToken = null, CancellationToken cancellationToken = default);

    /// <summary>Requests cancellation of a running/pending batch job.</summary>
    Task CancelAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes a batch job and its results.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls until the job reaches a terminal state (SUCCEEDED/FAILED/CANCELLED/EXPIRED). Throws
    /// GeminiTimeoutException if the timeout elapses first. Mirrors IFileService.WaitUntilActiveAsync.
    /// </summary>
    Task<BatchJob> WaitUntilCompleteAsync(
        string name,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a completed job's results. Works for both inline (already in the BatchJob) and
    /// file-based (downloads + parses the JSONL output) destinations. Throws if the job isn't in a
    /// terminal SUCCEEDED-with-results state.
    /// </summary>
    Task<IReadOnlyList<InlinedBatchResponse>> GetResultsAsync(BatchJob job, CancellationToken cancellationToken = default);
}
```

The interface has two create overloads and not one method with a nullable either-or parameter. This
matches the existing style of the codebase, which prefers explicit overloads over ambiguous "pick a
mode" parameters. For example, `ChatAsync` has distinct `MessageObject[]` and `IList<Content>`
overloads and not one loosely typed signature.

### 4.3 Wiring into DI (`Extensions/GeminiExtensions.cs`)

Add the following next to the other resource services:

```csharp
services.AddTransient<IBatchService, BatchService>();
```

`BatchService` should take a constructor dependency on `IGeminiClient` (for the create, get, list,
cancel, and delete calls on the versioned base address) and on `IFileService`. The service now needs
`IFileService` in three ways: to upload the JSONL file inside `CreateFromRequestsFileAsync`, and to
download and parse it inside `GetResultsAsync`. Inject `IFileService`. Do not reach around it. This
mirrors the pattern of `CachingService`, which depends only on `IGeminiClient`, and extends it with
the real cross-service need of this feature.

Also add a small private `Normalize(string name)` helper on `BatchService`. It adds the `"batches/"`
prefix when it is missing. It mirrors the `"files/"` prefixing of `FileService.Normalize` exactly.
Every method that takes a `name` (`GetAsync`, `CancelAsync`, `DeleteAsync`, and
`WaitUntilCompleteAsync`) should run its input through it, as `FileService` already does for every
method that takes a file name.

### 4.4 JSON source-generation registration

Add every new root type to the `[JsonSerializable(...)]` list in `GeminiJsonContext.cs`, per the
existing convention. Types nested inside `BatchJob` do not need separate entries, as is the case for
`Content` and `Part` today. Do not forget this. A type that is used in serialization but is missing
from the source-gen context either falls back silently to reflection (which defeats the AOT and trim
goal) or throws at runtime. Which one happens depends on how `GeminiJson.Default` is configured.
Check `Infrastructure/Serialization/GeminiJson.cs` to see which failure mode applies. Confirm that
the new types work before you consider this done.

### 4.5 `IFileService.DownloadFileAsync` (a new method on an existing interface)

```csharp
/// <summary>Downloads a file's raw bytes by resource name (e.g. a batch job's JSONL results file).</summary>
Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default);
```

Implement it in `FileService`, with the existing `_httpClient` (already the root-relative
`GeminiHttpClients.Files` client, see `Services/FileService.cs`):

```csharp
public async Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default)
{
    using var response = await _httpClient.GetAsync($"download/{Version}/{Normalize(name)}:download?alt=media", cancellationToken);
    await EnsureSuccessAsync(response, "download file", cancellationToken);
    return await response.Content.ReadAsByteArrayAsync(cancellationToken);
}
```

`Version` and `Normalize` are the existing private members that `FileService` already has. Reuse
them. Do not duplicate them.

### 4.6 JSONL parsing

Output files are not a single JSON document. They are newline-delimited JSON, with one object in
the shape of `InlinedBatchResponse` per line. The source-generated context of `System.Text.Json`
cannot deserialize that as one unit. Parse it manually:

```csharp
var text = Encoding.UTF8.GetString(bytes);
var results = new List<InlinedBatchResponse>();
foreach (var rawLine in text.Split('\n'))
{
    var line = rawLine.TrimEnd('\r'); // tolerate CRLF-served content, not just LF
    if (string.IsNullOrWhiteSpace(line)) continue;
    var parsed = JsonSerializer.Deserialize<InlinedBatchResponse>(line, _json);
    if (parsed is not null) results.Add(parsed);
}
```

Wrap deserialization failures in `GeminiSerializationException`, which matches every other parse
path in this codebase (see `FileService.Deserialize<T>`).

### 4.7 Error handling

Copy the `EnsureSuccessAsync` pattern of `FileService` for any raw-`HttpClient` calls (the download
path). For calls that go through `IGeminiClient.PostAsync`, `GetAsync`, or `DeleteAsync`, the mapping
of errors to `GeminiApiException` already happens inside `GeminiClient`. No separate error handling
is needed there, as in `CachingService` today.

`CancelAsync` posts to `batches/{id}:cancel` with an empty body. What it returns is uncertain. The
REST reference says an empty object. Google Cloud APIs conventionally return either
`google.protobuf.Empty` (literally `{}`) or the updated resource for `:cancel`-style methods, and no
corroborated source resolved this. Do not guess which one, and do not build around a guess. Define a
minimal `internal sealed class EmptyBatchResponse { }` (zero properties, all permissive) as the
`TResponse` for this one call:
`await _client.PostAsync<object, EmptyBatchResponse>($"{Normalize(name)}:cancel", new { }, cancellationToken)`.
Discard the result, and have `CancelAsync` return `Task` (not `Task<BatchJob>`). This deserializes
successfully whether Google returns `{}` or a full resource with extra fields. `System.Text.Json`
skips unmapped JSON members by default (`UnmappedMemberHandling.Skip`, the framework default). This
is separate from the `PropertyNameCaseInsensitive` setting that `GeminiJsonContext` already sets for
a different reason. Do not conflate the two. So a target type with zero properties never throws
only because the payload has more fields than the type declares. Remember to register
`EmptyBatchResponse` in `GeminiJsonContext.cs` (§4.4). It is a root type like everything else.

## 5. Model constants

Add batch-relevant model name references only if none already fit. Check the `Models` class in
`Junaid.GoogleGemini.Net/Infrastructure/Utilities/GeminiConstants.cs` first. Batch uses the exact
same model identifiers as interactive calls (`gemini-3.6-flash`, and so on). So you should need no
new constants, unless a batch-only model shows up in testing.

## 6. Telemetry

Do not wire `GeminiTelemetry.RecordUsage` or `RecordCost` into batch calls. Those exist to
instrument a single request and response round-trip. A batch job is create, then poll, then fetch.
Its "usage" exists only per line, after the fact, in the results. A count metric for batch job
creation (for example `gemini.client.batch.jobs_created`) would be a reasonable small addition, but
v1 does not require it. Note it as a possible follow-up in the ROADMAP entry. Do not build it
speculatively.

## 7. Testing strategy

Unit tests (`tests/Junaid.GoogleGemini.Net.Tests/Services/BatchServiceTests.cs`) use the existing
`FakeHttpMessageHandler` pattern. They cover:
- create (both modes), get, list (with and without paging parameters), cancel, and delete
- `WaitUntilCompleteAsync` (immediate success, timeout, and a FAILED terminal state)
- `GetResultsAsync` for both inline and file-based results (JSONL parsing, including a line with an
  `error` instead of a `response`, and a malformed line that produces `GeminiSerializationException`)

Live tests need unusual care, more than any other feature in this codebase, because of the timing in
§2.5: a target turnaround of 24 hours, no SLA, and up to 48 hours before expiry. CI, or an
interactive session, cannot reasonably wait on a live test that blocks on real completion.

- Add `tests/Junaid.GoogleGemini.Net.IntegrationTests/BatchLiveTests.cs`, `[Collection("Live")]`,
  `[RequiresGeminiKey]`. Use the same convention as every other live test: skip without a key.
- The test that resolves the state enum (§2.4) does not need to wait for completion. Create a tiny
  (1-request) inline batch job, call `GetAsync` on it at once, and assert on the raw `state` string
  that you observe. This resolves §2.4 in seconds and not hours. It is also the one live assertion
  that actually blocks you from finishing this plan responsibly. While you write this test, also
  confirm two other things that §2.3 flags as uncertain. Inspect the raw response body to do this
  (for example with a temporary breakpoint or log, and do not assert yet).
  - Confirm that the API accepts a create request body that has no `Model` field and has a `"batch"`
    wrapper. A 200 response with the job created is enough proof.
  - When convenient, note whether Google really returns an empty object from `CancelAsync`. This
    does not require waiting for completion. It is enough to call `CancelAsync` on a job and inspect
    once, manually, what came back through the underlying raw body of `EmptyBatchResponse`.
- A second live test may create a job and at once call `CancelAsync` and then `GetAsync` on it. It
  asserts that the state moves to the cancelled terminal value. This is also fast, with no 24-hour
  wait.
- The `fileName` or `responsesFile` question (§2.3) can be resolved only once a real file-mode job
  reaches a terminal state with results. That means waiting for real completion. Do not build a
  blocking automated test for this. Do it once, manually. We verified the aspect ratio for image
  generation manually in the same way (see the Phase 4 entry for `6.1.0` in the ROADMAP). Hardcode
  whichever field name you actually observe. If both fields somehow appear, prefer the one with a
  non-empty value.
- Do not write a live test that calls `WaitUntilCompleteAsync` with a multi-hour timeout and expects
  real completion. If end-to-end result retrieval needs live verification, do it manually, once,
  outside the automated suite. That is how the image-generation feature was "verified live end to
  end" per the ROADMAP, and that verification is not a standing CI test. Document what you checked in
  the ROADMAP entry (§10), using the convention that we already established.

## 8. Documentation

- Add `docs/articles/batch-api.md`, following the structure of `docs/articles/cost-governance.md`
  (mechanisms, code example, limitations section, scope section). It must include the following:
  - the two submission modes, with examples
  - the polling helper
  - the split in result retrieval (inline or file)
  - the discount
  - the 48-hour expiry and the 6-week retention facts
  - the paid-tier requirement (§2.7), stated explicitly. A free-tier user who hits
    `PERMISSION_DENIED` with no explanation has a bad first experience.
- Add the article to both `docs/articles/toc.yml` and the Guides list in `docs/index.md`. The sample
  publish pass found and fixed exactly this kind of omission for the image generation article. Do
  not reintroduce the same gap for this one.
- `README.md`: add one line in the feature list or comparison table. Do not duplicate a full
  section. The cleanup pass just removed a duplicated Cost governance section. Do not recreate that
  pattern.
- No em dashes or decorative unicode in any of the above (a standing style rule).

## 9. Follow-up work this plan deliberately does not do

List these in the ROADMAP entry that this plan produces. Name them as concrete follow-ups, and do
not write a vague "future work":
- Cost governance for batch jobs (it needs a batch-rate concept in `ModelPricing` first).
- Batch embeddings.
- Client-side pre-validation of the 20MB inline, 2GB file, and 100-concurrent-job limits.
- A `gemini.client.batch.jobs_created` (or similar) OpenTelemetry counter.
- An ASP.NET Core sample endpoint that demonstrates batch submission and polling.

## 10. Checklist

- [ ] `IBatchService` and `BatchService` (create-inline, create-from-file,
      `CreateFromRequestsFileAsync`, get, list, cancel, delete, `WaitUntilCompleteAsync`,
      `GetResultsAsync`).
- [ ] `CreateBatchRequest` has no `Model` property and wraps its payload in `"batch"` (§2.3).
- [ ] `IFileService.DownloadFileAsync` and its `FileService` implementation.
- [ ] New model types, named to avoid a collision with the existing `BatchEmbedContent*` types
      (§4.1). `InlinedBatchRequest` (inline, `Metadata`) stays distinct from `BatchRequestLine`
      (JSONL, `Key`).
- [ ] `state` modeled as `string` (not enum), per §2.4. Confirmed against a real API response before
      merging, and not assumed from docs.
- [ ] The file field of `BatchJobDestination` is named per whichever of `fileName` or
      `responsesFile` the manual live check (§7) actually observed.
- [ ] `EmptyBatchResponse` is used for the response of `CancelAsync` (§4.7) and registered in
      `GeminiJsonContext.cs`.
- [ ] `BatchService.Normalize` helper added (`"batches/"` prefix, mirrors `FileService.Normalize`).
- [ ] All new root types are registered in `GeminiJsonContext.cs`.
- [ ] DI registration in `GeminiExtensions.cs`.
- [ ] Unit tests (both the create-inline and create-from-file paths, the JSONL output of
      `CreateFromRequestsFileAsync`, get, list, cancel, delete, wait-until-complete including
      timeout and failure, and results parsing including error lines, malformed lines, and
      CRLF-terminated lines).
- [ ] Live tests: a fast create and get (resolves §2.4 and sanity-checks the request-body shape),
      and a fast create, cancel, and get. No multi-hour live test. One manual check of the
      destination file field name, documented in the ROADMAP entry and not encoded as a standing
      test.
- [ ] `docs/articles/batch-api.md`, linked from both `toc.yml` and `docs/index.md`.
- [ ] README feature mention (no duplicated section).
- [ ] ROADMAP.md entry under Phase 4. It includes the follow-up list from §9. It also records what
      the manual live check (§7) actually found for the two facts that docs could not resolve (§2.4,
      and the file field name in §2.3).
- [ ] The full solution builds with 0 warnings and 0 errors on net8.0, net9.0, and netstandard2.0.
- [ ] All existing tests still pass.
