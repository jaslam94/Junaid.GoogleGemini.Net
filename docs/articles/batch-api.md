# Batch API

The Batch API lets you submit large volumes of `generateContent` requests for asynchronous
processing. Google charges a discounted batch rate for them (50% of the standard interactive cost).
You trade latency for cost. Google targets a 24-hour turnaround, often faster, but gives no hard
SLA. A job that is still running after 48 hours expires automatically. Use batch for large,
non-urgent workloads such as bulk classification, evaluation runs, and offline data processing. Do
not use it for anything a user is waiting on.

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));
```

`AddGemini` registers `IBatchService` automatically, alongside `IGeminiService`.

## Before you start: you need a paid-tier project

Google documents the Batch API as unavailable on the Gemini free tier. If job creation fails with a
permission error, the free tier is the most likely cause. It is probably not a bug in your
integration.

## Submitting a job

`IBatchService` offers two ways to submit a job.

### Inline requests (small jobs)

```csharp
var job = await batchService.CreateAsync(
    "gemini-3.6-flash",
    new List<InlinedBatchRequest>
    {
        new() { Request = new GenerateContentRequest { Contents = { /* ... */ } } },
        new() { Request = new GenerateContentRequest { Contents = { /* ... */ } } },
    },
    displayName: "my-batch-job");
```

Google documents a ceiling of about 20MB total request size for inline requests. For anything
bigger, use one of the file-based paths below.

### From an in-memory list, through JSONL (recommended for most volumes)

`CreateFromRequestsFileAsync` writes your requests as a JSONL file, uploads it, and creates the job
in one call. You do not need to write Google's JSONL line format by hand:

```csharp
var job = await batchService.CreateFromRequestsFileAsync(
    "gemini-3.6-flash",
    new List<BatchRequestLine>
    {
        new() { Key = "row-1", Request = new GenerateContentRequest { Contents = { /* ... */ } } },
        new() { Key = "row-2", Request = new GenerateContentRequest { Contents = { /* ... */ } } },
    },
    displayName: "my-batch-job");
```

The `Key` on each line is optional, but we strongly recommend it for anything beyond a single
request. Google does not guarantee that file-mode output matches the input order. `Key` is the
reliable way to match a result to its request (see `InlinedBatchResponse.Key` below).

### From a file you already built or uploaded

If an external pipeline already built a JSONL file, reference it like this:

```csharp
var uploaded = await fileService.UploadFileAsync(jsonlBytes, "jsonl", "my-requests.jsonl");
var job = await batchService.CreateFromFileAsync("gemini-3.6-flash", uploaded.Name!, "my-batch-job");
```

File mode has a ceiling of 2GB per file.

## Waiting for completion

```csharp
var finished = await batchService.WaitUntilCompleteAsync(
    job.Name!,
    pollInterval: TimeSpan.FromMinutes(1),
    timeout: TimeSpan.FromHours(2)); // no sensible default exists; pick one that fits your workload
```

This polls `GetAsync` until the job reaches a terminal state: succeeded, failed, cancelled, or
expired. Google targets 24 hours and expires jobs after 48 hours, so do not block a web request on
this call. Run it from a background job, a queue worker, or similar. In live testing, a trivial
single-request job always completed in under two minutes. That is one data point on one small job.
Do not design around it. Use the documented 24-hour target for capacity planning.

If you poll manually, `BatchService.IsTerminalState(job.State)` is the same check that
`WaitUntilCompleteAsync` uses internally.

## Reading results

```csharp
var finished = await batchService.GetAsync(job.Name!);
var results = await batchService.GetResultsAsync(finished);

foreach (var result in results)
{
    if (result.Response is not null)
    {
        Console.WriteLine($"{result.Key}: {result.Response.Text()}");
    }
    else if (result.Error is not null)
    {
        Console.WriteLine($"{result.Key}: failed - {result.Error.Message}");
    }
}
```

`GetResultsAsync` handles both result destinations for you. If the job's results are inline, they
are already on the `BatchJob` object, and the method returns them directly. If they are file-based,
the method downloads and parses the JSONL output. Either way, you get the same
`IReadOnlyList<InlinedBatchResponse>`.

Call it on a completed job (see [Waiting for completion](#waiting-for-completion)), not one that is
still running. It throws if the job has no `Output` yet. It also throws if `Output` exists but has
neither inline responses nor a results file name. The method does not return an empty list in that
case. A silent empty list would look the same as a job that produced nothing, and it could hide a
job-level failure. If you hit this exception, check `BatchJob.Error`.

A batch job that succeeds as a whole can still contain failed requests. Check
`BatchJob.BatchStats.FailedRequestCount`. Also check each result's `Error`, as the loop above does.
One bad request in a 10,000-request batch does not fail the other 9,999.

## Cancelling and cleaning up

```csharp
await batchService.CancelAsync(job.Name!);
await batchService.DeleteAsync(job.Name!);
```

Cancellation is asynchronous on Google's side. A successful `CancelAsync` call means Google
accepted the request. It does not mean the job is cancelled the moment the call returns. Call
`GetAsync` afterward if you need to see the actual state change.

Google keeps job results for 6 weeks after completion, then deletes them permanently. This happens
whether or not you called `DeleteAsync`.

## What this library does not do for you

- Enforce Google's size and concurrency limits client-side. The library leaves the ~20MB inline
  ceiling, the ~2GB file ceiling, and the documented limit of 100 concurrent jobs to the API. It
  does not check any of them. If you go over, you get a normal `GeminiApiException`.
- Cost governance. The daily budget and per-request estimate of `ICostGovernor` (see
  [Cost governance](cost-governance.md)) do not apply to batch jobs. Batch responses carry real
  usage data once results come back. That usage is at the discounted batch rate, which this
  library's pricing model does not yet represent. Batch spend tracking may come in a future
  release. This release does not have it.
- Rate limiting. Batch calls bypass `IRateLimiter` on purpose. That limiter is tuned for interactive
  per-minute request limits, and batch draws from a separate quota pool on Google's side. Retries
  still apply (see [Resilience & rate limiting](resilience-and-rate-limiting.md)).
- OpenTelemetry instrumentation. Batch calls emit no spans and no duration, token, or cost metrics.
  `IBatchService` uses its own `HttpClient` instead of the shared pipeline that emits them (see
  [Observability](observability.md)).
- Batch embeddings or batch image generation. This release covers `generateContent` only.

## The state string

`BatchJob.State` is a plain string, not a C# enum. Real responses use a `BATCH_STATE_*` prefix
(`BATCH_STATE_PENDING`, `BATCH_STATE_RUNNING`, `BATCH_STATE_SUCCEEDED`, `BATCH_STATE_CANCELLED`).
We confirmed this live against the real API. Google's guide and cookbook samples show `JOB_STATE_*`
instead. That naming turned out to belong to the Python SDK, not the raw wire format.

The library keeps `State` as a plain string and does not hardcode an enum. `BatchService.IsTerminalState`
checks the suffix (`SUCCEEDED`, `FAILED`, `CANCELLED`, `EXPIRED`) and not the full literal. Your
code then keeps working if the naming changes again. For the same reason, do not compare `State`
against a full literal such as `"BATCH_STATE_SUCCEEDED"`. Use `IsTerminalState` or a suffix check.

## The response shape (if you read raw JSON)

You do not need this for normal use of `IBatchService`, because the library handles it. It matters
only if you inspect raw API responses directly.

Create, Get, and List responses come wrapped in a Google long-running-operation envelope
(`{ name, metadata, done, error | response }`). They are not a flat batch resource. The fields you
would expect at the root (`state`, `batchStats`, `displayName`, `output`, and so on) live under
`metadata`. Google's guide, REST reference, and cookbook do not document this for the feature. We
confirmed it only by making real calls. The properties of `BatchJob` (`State`, `Output`,
`BatchStats`, and others) already read from the right place. So this matters only if you work with
the wire format directly.
