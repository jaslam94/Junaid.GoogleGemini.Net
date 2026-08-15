# Plan: Batch API for Junaid.GoogleGemini.Net

**Status:** Implemented and live-verified in `6.4.0` (PR #42) on 2026-08-15. Kept here as a
historical record, same convention as `PLAN-cost-governance.md`.

**Live-verification addendum (2026-08-15, after a real key was finally used):** this plan's own
epistemic-hygiene effort (the self-review pass, the source-reliability calibration, hedging §2.4/§2.3
as unresolved rather than guessing) was the right instinct but insufficient on its own. Live testing
found the two flagged facts BOTH resolved differently than this plan's working assumptions, plus a
third, more serious gap none of the three research sources (guide, REST reference, cookbook) surfaced
as central: **Create/Get/List responses are wrapped in a Google long-running-operation envelope**
(`{ name, metadata, done, error | response }`), not the flat resource this entire plan assumed
throughout §2.3 and §4. The real batch fields live under `metadata`. Against the real API, the
originally-shipped flat `BatchJob` would have deserialized `State`/`BatchStats`/`DisplayName` as
permanently null, meaning `WaitUntilCompleteAsync` would loop until timeout on every real job. One of
this plan's own early research fetches *did* mention an "Operation" shape (§ omitted from the final
plan text as apparently tangential) - it was the actual answer, misfiled as a side note. Lesson for next
time: when research surfaces a shape that doesn't obviously fit the working model, don't file it away:
figure out where it fits before continuing. See `ROADMAP.md`'s `6.4.0` entry for the full list of what
live testing corrected (state prefix, results-file field name, the operation-envelope restructure, and
`batchStats`' string-typed numbers) and exactly how it was verified (raw REST calls independent of this
library's own types, plus the actual library code, run against real jobs taken to real completion).

**Post-implementation addendum (pre-live-verification, still accurate):**
`CancelAsync` ended up simpler than §4.7 specified — rather than deserializing into the
`EmptyBatchResponse` type this plan describes, the actual implementation discards the cancel response
body entirely (success/failure is determined purely by the HTTP status code), so `EmptyBatchResponse`
was never added to the codebase. The reasoning in §4.7 for *why* the response shape can't be trusted
still holds; it just turned out there was no need to model it at all once the implementation didn't try
to read it. A second, unplanned addition: `GetResultsAsync` throws (rather than returning an empty
list) when a job's `Output` object exists but has neither inline responses nor a results file name,
found during a second-pass review after implementation — an empty list there would look identical to a
legitimately empty result set and could silently mask the exact field-name risk §2.3 flags. Everything
else below matches what shipped. **Audience:** an AI coding agent (Cursor) implementing this directly against
the current `master` branch (post-6.3.1). **Author's confidence:** every file path, method signature,
and integration point below was verified by reading the actual current source on 2026-08-15. The
Gemini Batch API's own shape was researched from Google's live docs on the same date (see §2), and
**this plan has already been through one self-review pass** that caught several wrong or unverifiable
claims from the first draft (a fabricated citation, a wire-format field-name error, a self-contradiction
about the state field's type) — all are corrected below and marked with what evidence actually supports
them. Two material facts (§2.4's state-string values, §2.3's destination-file field name) could not be
resolved with full confidence from docs alone and **must be confirmed against a real API response**
before those specific details are finalized in code. Do not guess; §2.4 and §7 say exactly how to
resolve both cheaply, in the same live test.

**A note on source reliability, learned during this research:** the REST reference page
(`ai.google.dev/api/batch-mode`) was fetched twice, independently, and both times produced at least
one detail that direct evidence from other sources (the guide's own worked examples, the official
cookbook notebook's actual printed API output) contradicts. Treat any fact below sourced *only* from
that page, with no corroboration, as the weakest-confidence tier — those are called out individually
below rather than presented with the same confidence as corroborated facts.

---

## 1. Goal

Let a caller submit large volumes of `generateContent` requests asynchronously, at Google's
advertised 50% cost discount, without hand-rolling the JSONL file protocol, polling loop, or result
parsing. This is a new **resource client** (`IBatchService`), following the same layering the
codebase already uses for Files (`IFileService`) and context caching (`ICachingService`): a thin,
faithful 1:1 mapping onto the batch endpoints, not folded into the `IGeminiService` facade. Batch jobs
are a fundamentally different usage pattern (create-then-poll-then-fetch, not request-response), so
they get their own service, matching principle 1 in `ROADMAP.md`'s guiding principles.

## 2. What the Batch API actually is (researched 2026-08-15)

Cross-checked against three independent sources: the official guide
(`https://ai.google.dev/gemini-api/docs/batch-api`), the REST reference
(`https://ai.google.dev/api/batch-mode`), and the official cookbook notebook
(`google-gemini/cookbook`, `quickstarts/Batch_mode.ipynb`). Where sources disagreed, that's called out
explicitly rather than silently picking one.

### 2.1 Endpoints

| Operation | Method | Path |
|---|---|---|
| Create | `POST` | `models/{model}:batchGenerateContent` |
| Get | `GET` | `batches/{batchId}` |
| List | `GET` | `batches` |
| Cancel | `POST` | `batches/{batchId}:cancel` |
| Delete | `DELETE` | `batches/{batchId}` |

All relative to the same versioned base address `IGeminiClient` already targets (i.e. **do not**
prefix these with `v1beta/` in code — `CachingService`'s `"cachedContents"` endpoint is the pattern to
copy verbatim, since `GeminiClient`'s `HttpClient.BaseAddress` already includes the version segment).

Downloading a **file-based** result is a *different* path, rooted at the host authority rather than
the versioned base address:

```
GET /download/v1beta/{fileName}:download?alt=media
```

This is the same shape `FileService` already uses for uploads (`upload/v1beta/files`), which is why
it uses a separate root-relative `HttpClient` (`GeminiHttpClients.Files`). See §4.5 — this plan adds a
`DownloadFileAsync` method to `IFileService`, not `IBatchService`, since it's a Files API capability
that batch happens to depend on (and is independently useful for any large file, not just batch
output).

### 2.2 Submitting a batch: two mutually exclusive modes

1. **Inline requests** — a list of `GenerateContentRequest` objects embedded directly in the create
   call. Google's documented ceiling: **under 20MB** total request size.
2. **File input (JSONL)** — upload a JSON Lines file first via the existing Files API
   (`IFileService.UploadFileAsync`, MIME type `"jsonl"`), then reference it by name. Ceiling: **2GB**
   per file. This is the recommended path for anything non-trivial.

JSONL input line shape (one `GenerateContentRequest` per line, optionally keyed):

```json
{"key": "request-1", "request": {"contents": [{"parts": [{"text": "..."}]}]}}
```

The `key` is caller-supplied and echoed back on the matching output line — this is the only way to
correlate an output line back to its input when using **file** mode, since output order is not
documented as guaranteed to match input order. This is a JSONL-only concept (see §2.3's correction —
inline mode's per-item field is `metadata`, not `key`; the two modes are not interchangeable here).

**Gap this creates, closed in §4.2:** the stated Goal (§1) is that callers shouldn't have to hand-roll
the JSONL protocol themselves. But if `IBatchService` only offers `CreateFromFileAsync(model, fileName,
...)` for an *already-uploaded* file, a caller with an in-memory list of requests still has to
hand-write JSONL lines and call `IFileService.UploadFileAsync` themselves before they can use it — the
exact thing the Goal says this feature should spare them. §4.2 adds a convenience method that writes
the JSONL and uploads it internally, so this promise is actually kept for both submission modes.

### 2.3 The `Batch` resource (get/list/create response shape)

```
name              string   "batches/123456789"                  (output only)
displayName       string
model             string   "models/gemini-3.6-flash"             (echoed back on Get/List; see below)
inputConfig       object   oneof: fileName (string) | requests (InlinedRequest[])
output / dest     object   oneof: fileName (string) | inlinedResponses (InlinedResponse[])  — see note
createTime        string   RFC 3339
updateTime        string   RFC 3339
endTime           string   RFC 3339                              (set once terminal)
batchStats        object   requestCount, successfulRequestCount, failedRequestCount, pendingRequestCount
priority          int64    optional, create-time only (documented but not used by v1 — see §3)
state             string   see §2.4
```

**On `model` and the create request body:** the guide's own cURL example for creating a file-based
batch job sends `{"batch": {"display_name": ..., "input_config": {"file_name": ...}}}` — **no
`model`/`display_model` field in the body at all.** The model is expressed only in the URL
(`models/{model}:batchGenerateContent`), matching how every other endpoint in this codebase already
builds request URLs (`GenerateContentRequest` has no `Model` field either, for the same reason). So
`CreateBatchRequest`'s body must **not** include a model field; `model` only shows up as a field when
*reading back* a `BatchJob` via Get/List, populated server-side from what the URL was at creation.
Building the create request with a `Model` property (an earlier draft of this plan did) would be
wrong — remove it from `CreateBatchRequest` entirely.

**The create request body has a top-level `"batch"` wrapper** — confirmed directly from both the
guide's cURL example and the REST reference's method signature
(`{batch.model=models/*}:batchGenerateContent`). `CreateBatchRequest` must serialize as
`{ "batch": { "displayName": ..., "inputConfig": {...} } }`, not a bare `{ "displayName": ..., ... }`.
This is easy to miss and would silently produce a request Google's API rejects or ignores.

**Field-name discrepancy, unresolved — `fileName` vs `responsesFile` for the output/destination
file field:** the guide's own worked JSON sketch (`"dest": {"fileName": "files/output123", ...}`) and
the *Python SDK's actual code sample* (`batch_job.dest.file_name`, which only makes sense if the
underlying JSON field is `fileName`) both point to **`fileName`**. Only the REST reference page (the
source already shown unreliable once, on the state enum — see §2.4) says `responsesFile`. Given that
source's track record here, prefer `fileName` as the working assumption, but **do not ship this
without confirming it against a real response** — same live test that resolves §2.4 (§7) should assert
which key is actually present in a real `output`/`dest` object once a job completes, or at minimum log
the raw JSON once during manual verification.

`InlinedRequest`: `{ request: GenerateContentRequest, metadata?: object }` — **`metadata`**, not
`key`, is the documented wrapper field name for **inline** mode. `key` is a JSONL-line-only concept
for **file** mode (see §2.2) — these are two different envelopes around the same `request` payload,
not the same field under two names. Don't reuse one type for both without accounting for that;
`InlinedBatchRequest` (§4.1) models the inline shape only.

`InlinedResponse`: `{ metadata?: object, response?: GenerateContentResponse, error?: Status }` — a
oneof between `response` and `error`, exactly like the per-line JSONL output (`{"response": ...}` or
`{"error": {"code": ..., "message": ...}}`).

### 2.4 State enum — UNRESOLVED, resolve before finalizing

Two different naming schemes turned up depending on the source:

- The **user guide**, the **cookbook notebook's actual printed cell output** (`batch_job.state.name`
  from real API calls), and multiple corroborating forum threads all show:
  `JOB_STATE_PENDING`, `JOB_STATE_RUNNING`, `JOB_STATE_SUCCEEDED`, `JOB_STATE_FAILED`,
  `JOB_STATE_CANCELLED`, `JOB_STATE_EXPIRED`.
- The **REST API reference page**, fetched twice independently, both times showed a `BATCH_STATE_*`
  prefix instead.
- One old (lower-numbered, likely 2025-era) developer-forum thread also used `BATCH_STATE_RUNNING`,
  suggesting the API may have been renamed from `BATCH_STATE_*` to `JOB_STATE_*` at some point, with
  stale caches of the REST reference still showing the old name.

**Do not pick one from docs alone.** Resolve it the same way this codebase already resolved a similar
docs-vs-reality gap for `thoughtSignature` (see `PLAN-cost-governance.md`'s live-verification
precedent): the very first live test written for this feature (§7) must assert on the *actual* string
in `state` from a real `batches.get` response and that observed value is what goes in the shipped
enum/constants. If both prefixes are somehow accepted by the API (i.e. it's case/alias-tolerant),
prefer whichever the guide documents (`JOB_STATE_*`), since that's the user-facing contract. Model
`state` as a `string` on the wire type (not a C# `enum`), with `GeminiConstants` string constants for
the known values — exactly the existing pattern for `FileResource.State` (`"ACTIVE"`, `"FAILED"`,
etc. are plain strings, matched via `string.Equals(..., StringComparison.OrdinalIgnoreCase)` in
`FileService.WaitUntilActiveAsync`). This sidesteps the naming ambiguity entirely: whichever prefix
the live API actually returns, a string-typed field round-trips it correctly, and callers doing
ordinal-insensitive comparison (as the `WaitUntilCompleteAsync` helper below will) work either way.

### 2.5 Terminal states, timing, retention

- Terminal states: `SUCCEEDED`, `FAILED`, `CANCELLED`, `EXPIRED` (whatever the resolved prefix, these
  four suffixes are consistent across every source).
- A job auto-expires (`EXPIRED`) if it's still `PENDING`/`RUNNING` after **48 hours**.
- Target turnaround is "24 hours, often faster" — **not a hard SLA**, no documented minimum.
- Results (both inline and file-based) are retrievable for **6 weeks** after completion, then deleted.
- Job creation is explicitly documented as **not idempotent** — calling create twice with the same
  logical content produces two separate jobs. No dedup/upsert-by-displayName exists.

### 2.6 Pricing

Flat **50% discount** vs. the standard (interactive) rate, confirmed to hold proportionally across
every pricing tier checked (including the >200k-token long-context tiers for `gemini-2.5-pro` /
`gemini-3.1-pro-preview`), per the live pricing page fetched the same day as this research.
**Cost-governance integration is explicitly out of scope for this plan** — see §3's exclusions and §9
for why, and what a follow-up would need.

### 2.7 Access requirements and limits

- Batch API requires a **paid-tier** project; it is not available on the free tier.
- Batch jobs draw from a **separate quota pool** from interactive requests (own concurrent-job, file,
  storage, and enqueued-token limits) — this library's existing `IRateLimiter`/`ICostGovernor`, both
  scoped to interactive calls, are correctly left untouched by this feature (see §3).
- Documented ceiling: **100 concurrent batch jobs** per project (as of the source checked; treat as
  informational only — this library does not enforce it, the API will reject over-limit creates).
- Batch API currently supports `generateContent` only (this plan's scope). Cookbook examples also show
  a separate `create_embeddings` batch surface and image-generation-in-batch (same endpoint, just
  `responseModalities` in the per-request config) — see §3 for what's in/out.

## 3. Explicit scope

**In scope (v1):**
- `IBatchService` covering create (inline and file-input), get, list, cancel, delete.
- Both submission modes for `generateContent`-shaped requests, including requests that themselves use
  system instructions, tools, and generation config (i.e. `GenerateContentRequest`, the existing wire
  model — no new request-shaping logic needed, only a new envelope around it).
- A `WaitUntilCompleteAsync` polling helper, mirroring `IFileService.WaitUntilActiveAsync`'s exact
  shape (`pollInterval`, `timeout`, cancellation).
- Result retrieval for both inline (`dest.inlinedResponses`, already in the Get response) and
  file-based (the destination file field, name TBD per §2.3 — requires the new
  `IFileService.DownloadFileAsync` + JSONL parsing) outcomes.
- A new `IFileService.DownloadFileAsync(string name, CancellationToken ct)` method (returns raw
  `byte[]` or a `string`; see §4.5), since retrieving file-based batch output requires it and no
  existing method covers "download arbitrary file bytes by name."
- A JSONL-writing convenience method (§4.2's `CreateFromRequestsFileAsync`) that builds the JSONL
  payload from an in-memory list of requests and uploads it via `IFileService`, so file-mode submission
  doesn't require the caller to hand-write the JSONL protocol either — see §2.2's gap note for why this
  was added on this review pass and isn't optional polish.

**Explicitly out of scope for this change (do not implement):**
- **Cost governance integration.** `ICostGovernor` prices interactive calls in real time from a single
  response's `UsageMetadata`. Batch responses carry `usageMetadata` too (confirmed in the cookbook's
  output JSONL example), so a batch job's cost *could* be computed after the fact — but at the batch
  (discounted) rate, which today's `ModelPricing` has no concept of (it's a flat rate per model, not
  rate-plus-batch-multiplier). Bolting this on half-finished would be worse than not touching it. Note
  it as a real, sized follow-up in the ROADMAP entry this plan produces (§10), don't attempt it here.
- **Batch embeddings** (`create_embeddings`). Different request/response shape, different model
  (`gemini-embedding-*`), and this library's `EmbedContentResponse` already has no usage/token field
  (the same gap that excluded embeddings from cost governance) — separate design work.
- **Webhooks** (`client.webhooks.create(...)` as an alternative to polling). Found in the cookbook but
  not in the primary guide; looks newer/less battle-tested, and this library has no existing
  webhook-receiving story to hang it on (it's a client library, not a hosted service). Polling via
  `WaitUntilCompleteAsync` is sufficient for v1.
- **Rate limiting integration.** Batch draws from a separate quota pool (§2.7); routing batch calls
  through `IRateLimiter` (tuned for interactive RPM) would be actively wrong. Batch calls bypass the
  rate limiter entirely, same as `IFileService` already does today.
- **Enforcing the 100-concurrent-job or 20MB/2GB limits client-side.** Let the API reject over-limit
  requests with its normal error response; this library surfaces that via the existing
  `GeminiApiException` path, same as every other endpoint. Client-side pre-validation of size limits
  is a nice-to-have, not required for v1 — flag as a possible follow-up, don't build it now.
- **A sample endpoint in `AspNetCoreSample`.** Worth doing once the feature ships and is live-verified,
  but is a separate, smaller follow-up (same reasoning as why the cost-governance sample work was done
  as its own pass after 6.2.0 shipped, not bundled into it).

## 4. Architecture

### 4.1 New files

```
Junaid.GoogleGemini.Net/Services/Interfaces/IBatchService.cs
Junaid.GoogleGemini.Net/Services/BatchService.cs
Junaid.GoogleGemini.Net/Models/GoogleApi/BatchJob.cs           (Batch resource + nested types)
Junaid.GoogleGemini.Net/Models/GoogleApi/BatchJobList.cs       (list response)
Junaid.GoogleGemini.Net/Models/Requests/CreateBatchRequest.cs  (create request envelope)
```

**Naming note — read before naming anything:** `Models/GoogleApi/BatchEmbedContentResponse.cs`
already exists, for the unrelated *synchronous* multi-embedding call (`EmbedContentAsync`'s batch
variant, a single request/response pair, nothing to do with this feature). Do **not** name any new
type `Batch...ContentRequest`/`Response` — it will collide in meaning even if not in compiler symbol.
Suggested names: `BatchJob` (the resource itself, avoiding the bare `Batch` collision risk with the
`GeminiOptions.Budget`-adjacent `BudgetOptions` vocabulary too), `BatchJobSource` (the `inputConfig`
oneof), `BatchJobDestination` (the `output`/`dest` oneof), `InlinedBatchRequest` (the **inline**-mode
per-item envelope: `Request` + `Metadata`, no `Key`), `InlinedBatchResponse`, `BatchStats`,
`BatchJobList`, and `BatchRequestLine` (a small type used **only** by
`CreateFromRequestsFileAsync`/JSONL writing: `Key` + `Request` — do not merge this with
`InlinedBatchRequest`, they serialize to different wire shapes per §2.3's correction).

`CreateBatchRequest` (the create-call body) wraps its payload in a top-level `"batch"` property and
has **no `Model` property at all** — see §2.3's correction. Its shape is effectively
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
    /// a caller with an in-memory list never has to hand-write the JSONL protocol themselves (see §2.2)
    /// — the same reason CreateAsync exists for the inline path.
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

Two create overloads (not one method with a nullable-either-or parameter) matches this codebase's
existing style of preferring explicit overloads over ambiguous "pick a mode" parameters (see how
`ChatAsync` has distinct `MessageObject[]` and `IList<Content>` overloads rather than one loosely-typed
signature).

### 4.3 Wiring into DI (`Extensions/GeminiExtensions.cs`)

Add next to the other resource services:

```csharp
services.AddTransient<IBatchService, BatchService>();
```

`BatchService` should take a constructor dependency on `IGeminiClient` (for the versioned-base-address
create/get/list/cancel/delete calls) **and** `IFileService` (needed three ways now: uploading the
JSONL file inside `CreateFromRequestsFileAsync`, and downloading + parsing it inside
`GetResultsAsync`) — inject `IFileService`, don't reach around it. This mirrors `CachingService`'s
single-dependency-on-IGeminiClient pattern, extended with the genuine cross-service need this feature
has. Also add a small private `Normalize(string name)` helper on `BatchService` that prefixes
`"batches/"` when missing, mirroring `FileService.Normalize`'s `"files/"` prefixing exactly — every
`name`-taking method (`GetAsync`, `CancelAsync`, `DeleteAsync`, `WaitUntilCompleteAsync`) should run
its input through it, same as `FileService` already does for every file-name-taking method.

### 4.4 JSON source-generation registration

Add every new root type to `GeminiJsonContext.cs`'s `[JsonSerializable(...)]` list, per the existing
convention (nested types inside `BatchJob` don't need separate entries, same as `Content`/`Part`
today). Do not forget this: a type used in serialization but missing from the source-gen context
either falls back silently to reflection (defeating the AOT/trim goal) or throws at runtime depending
on how `GeminiJson.Default` is configured — check `Infrastructure/Serialization/GeminiJson.cs` for
which failure mode applies and confirm the new types work before considering this done.

### 4.5 `IFileService.DownloadFileAsync` (new method on an existing interface)

```csharp
/// <summary>Downloads a file's raw bytes by resource name (e.g. a batch job's JSONL results file).</summary>
Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default);
```

Implementation in `FileService`, using the existing `_httpClient` (already the root-relative
`GeminiHttpClients.Files` client — see `Services/FileService.cs`):

```csharp
public async Task<byte[]> DownloadFileAsync(string name, CancellationToken cancellationToken = default)
{
    using var response = await _httpClient.GetAsync($"download/{Version}/{Normalize(name)}:download?alt=media", cancellationToken);
    await EnsureSuccessAsync(response, "download file", cancellationToken);
    return await response.Content.ReadAsByteArrayAsync(cancellationToken);
}
```

(`Version` and `Normalize` are the existing private members already in `FileService` — reuse them, do
not duplicate.)

### 4.6 JSONL parsing

Output files are **not** a single JSON document — they're newline-delimited JSON, one
`InlinedBatchResponse`-shaped object per line, which `System.Text.Json`'s source-generated context
cannot deserialize as a unit. Parse manually:

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

Wrap deserialization failures in `GeminiSerializationException`, matching every other parse path in
this codebase (see `FileService.Deserialize<T>`).

### 4.7 Error handling

Copy `FileService`'s `EnsureSuccessAsync` pattern for any raw-`HttpClient` calls (the download path).
For calls that go through `IGeminiClient.PostAsync`/`GetAsync`/`DeleteAsync`, error mapping to
`GeminiApiException` already happens inside `GeminiClient` — no separate error handling needed there,
same as `CachingService` today.

`CancelAsync` posts to `batches/{id}:cancel` with an empty body. What it returns is genuinely uncertain
(the REST reference says an empty object; Google Cloud APIs conventionally return either
`google.protobuf.Empty` — literally `{}` — or the updated resource for `:cancel`-style methods, and
this wasn't resolved by any corroborated source). Don't guess which and build around it — define a
minimal `internal sealed class EmptyBatchResponse { }` (zero properties, all-permissive) as the
`TResponse` for this one call: `await _client.PostAsync<object, EmptyBatchResponse>($"{Normalize(name)}:cancel", new { }, cancellationToken)`,
discard the result, and have `CancelAsync` return `Task` (not `Task<BatchJob>`). This deserializes
successfully whether Google returns `{}` or a full resource with extra fields: `System.Text.Json`
skips unmapped JSON members by default (`UnmappedMemberHandling.Skip`, the framework default, distinct
from the `PropertyNameCaseInsensitive` setting `GeminiJsonContext` already sets for a different reason
— don't conflate the two), so a zero-property target type never throws just because the payload has
more fields than it declares. Remember to register `EmptyBatchResponse` in `GeminiJsonContext.cs`
(§4.4) — it's a root type like everything else.

## 5. Model constants

Add batch-relevant model name references only if none already fit — check
`Junaid.GoogleGemini.Net/Infrastructure/Utilities/GeminiConstants.cs`'s `Models` class first; batch
uses the exact same model identifiers as interactive calls (`gemini-3.6-flash`, etc.), so no new
constants should be needed unless a batch-only model shows up in testing.

## 6. Telemetry

Do **not** wire `GeminiTelemetry.RecordUsage`/`RecordCost` into batch calls. Those exist to instrument
a single request/response round-trip; a batch job is create-then-poll-then-fetch, and its "usage" only
exists per-line, after the fact, in the results. Emitting a *count* metric for batch job creation
(e.g. `gemini.client.batch.jobs_created`) would be a reasonable small addition but is not required for
v1 — note as a possible follow-up in the ROADMAP entry, don't build it speculatively.

## 7. Testing strategy

**Unit tests** (`tests/Junaid.GoogleGemini.Net.Tests/Services/BatchServiceTests.cs`), using the
existing `FakeHttpMessageHandler` pattern: cover create (both modes), get, list (with/without paging
params), cancel, delete, `WaitUntilCompleteAsync` (immediate success, timeout, a FAILED terminal
state), and `GetResultsAsync` for both inline and file-based (JSONL parsing, including a line with an
`error` instead of a `response`, and a malformed line producing `GeminiSerializationException`).

**Live tests** need unusual care, more than any other feature in this codebase, because of §2.5's
timing: target 24h turnaround, no SLA, up to 48h before expiry. A live test that blocks on real
completion is not something CI (or an interactive session) can reasonably wait on.

- Add `tests/Junaid.GoogleGemini.Net.IntegrationTests/BatchLiveTests.cs`, `[Collection("Live")]`,
  `[RequiresGeminiKey]` — same skip-without-a-key convention as every other live test.
- **The state-enum-resolving test (§2.4) does not need to wait for completion.** Create a tiny
  (1-request) inline batch job, immediately `GetAsync` it, and assert on the raw `state` string
  observed. This resolves §2.4 in seconds, not hours, and is the one live assertion that actually
  blocks finishing this plan responsibly. While this test is being written, also confirm (by inspecting
  the raw response body, e.g. via a temporary breakpoint/log, not by asserting yet) two other things
  flagged uncertain in §2.3: that the create request body without a `Model` field and with a `"batch"`
  wrapper is actually accepted (a 200 response with the job created is sufficient proof), and, once
  convenient (doesn't require waiting for completion — `CancelAsync` a job and inspect what came back
  through `EmptyBatchResponse`'s underlying raw body once, manually, is enough) note whether Google
  really does return an empty object there.
- A second live test may create a job and immediately `CancelAsync` + `GetAsync` it, asserting the
  state transitions to the cancelled terminal value — also fast, no 24h wait.
- The `fileName`-vs-`responsesFile` question (§2.3) can only be resolved once a real file-mode job
  reaches a terminal state with results, which does mean waiting for real completion. Don't build a
  blocking automated test for this — do it once, manually, the same way image generation's aspect
  ratio was manually verified (ROADMAP's Phase 4 entry for `6.1.0`), and hardcode whichever field name
  is actually observed. If both fields somehow appear, prefer the one with a non-empty value.
- Do **not** write a live test that calls `WaitUntilCompleteAsync` with a multi-hour timeout expecting
  real completion. If end-to-end result retrieval needs live verification, do it manually, once,
  outside the automated suite (same as how the image-generation feature was "live-verified end-to-end"
  per the ROADMAP but that verification isn't a standing CI test) — document what was checked in the
  ROADMAP entry (§10), same convention already established.

## 8. Documentation

- New `docs/articles/batch-api.md`, following the structure of `docs/articles/cost-governance.md`
  (mechanisms, code example, limitations section, scope section). Must include: the two submission
  modes with examples, the polling helper, the result-retrieval split (inline vs file), the discount,
  the 48-hour expiry / 6-week retention facts, and explicitly the paid-tier requirement (§2.7) — a
  free-tier user hitting `PERMISSION_DENIED` with no explanation is a bad first experience.
- Add the article to **both** `docs/articles/toc.yml` and `docs/index.md`'s Guides list — the sample
  publish pass found and fixed exactly this kind of omission for the Image generation article; don't
  reintroduce the same gap for this one.
- `README.md`: one line in the feature list/comparison table, no full section duplication (the
  cleanup pass just removed a duplicated Cost governance section — don't recreate the pattern).
- No em dashes or decorative unicode in any of the above (standing style rule).

## 9. Follow-up work this plan deliberately does not do

List these in the ROADMAP entry this plan produces, as concrete named follow-ups, not vague "future
work":
- Cost governance for batch jobs (needs a `ModelPricing` batch-rate concept first).
- Batch embeddings.
- Client-side pre-validation of the 20MB inline / 2GB file / 100-concurrent-job limits.
- A `gemini.client.batch.jobs_created` (or similar) OpenTelemetry counter.
- An ASP.NET Core sample endpoint demonstrating batch submission + polling.

## 10. Checklist

- [ ] `IBatchService` + `BatchService` (create-inline, create-from-file,
      `CreateFromRequestsFileAsync`, get, list, cancel, delete, `WaitUntilCompleteAsync`,
      `GetResultsAsync`).
- [ ] `CreateBatchRequest` has no `Model` property and wraps its payload in `"batch"` (§2.3).
- [ ] `IFileService.DownloadFileAsync` + `FileService` implementation.
- [ ] New model types, named to avoid collision with existing `BatchEmbedContent*` types (§4.1);
      `InlinedBatchRequest` (inline, `Metadata`) kept distinct from `BatchRequestLine` (JSONL, `Key`).
- [ ] `state` modeled as `string` (not enum), per §2.4 — confirmed against a real API response before
      merging, not assumed from docs.
- [ ] `BatchJobDestination`'s file field named per whichever of `fileName`/`responsesFile` the manual
      live check (§7) actually observed.
- [ ] `EmptyBatchResponse` used for `CancelAsync`'s response (§4.7), registered in `GeminiJsonContext.cs`.
- [ ] `BatchService.Normalize` helper added (`"batches/"` prefix, mirrors `FileService.Normalize`).
- [ ] All new root types registered in `GeminiJsonContext.cs`.
- [ ] DI registration in `GeminiExtensions.cs`.
- [ ] Unit tests (both create-inline and create-from-file paths, `CreateFromRequestsFileAsync`'s JSONL
      output, get, list, cancel, delete, wait-until-complete incl. timeout/failure, results parsing
      incl. error lines, malformed lines, and CRLF-terminated lines).
- [ ] Live tests: fast create+get (resolves §2.4 and sanity-checks the request-body shape), fast
      create+cancel+get. No multi-hour live test. One-time manual check of the destination file field
      name, documented in the ROADMAP entry rather than encoded as a standing test.
- [ ] `docs/articles/batch-api.md`, linked from both `toc.yml` and `docs/index.md`.
- [ ] README feature mention (no duplicated section).
- [ ] ROADMAP.md entry under Phase 4, including the §9 follow-up list and what the manual live check
      (§7) actually found for the two unresolved-from-docs facts (§2.4, §2.3's file field name).
- [ ] Full solution builds with 0 warnings/errors on net8.0/net9.0/netstandard2.0.
- [ ] All existing tests still pass.
