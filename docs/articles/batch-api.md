# Batch API

Submit large volumes of `generateContent` requests for asynchronous processing at Google's
discounted batch rate (50% of the standard interactive cost). This trades latency for cost: Google's
target turnaround is 24 hours, often faster, with no hard SLA, and a job auto-expires if it's still
running after 48 hours. Use it for large, non-urgent workloads (bulk classification, evaluation runs,
offline data processing), not for anything a user is waiting on.

```csharp
builder.Services.AddGemini(builder.Configuration.GetSection("Gemini"));
```

`IBatchService` is registered automatically alongside `IGeminiService`.

## Before you start: this needs a paid-tier project

The Batch API is documented as unavailable on the Gemini free tier. If job creation fails with a
permission error, that's most likely why, not a bug in your integration.

## Submitting a job

There are two ways in, both exposed on `IBatchService`:

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

Google's documented ceiling for inline requests is under ~20MB total request size. For anything
bigger, use one of the file-based paths below.

### From an in-memory list, via JSONL (recommended for most volumes)

`CreateFromRequestsFileAsync` writes your requests as a JSONL file, uploads it, and creates the job in
one call. You never have to hand-write Google's JSONL line format yourself:

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

The `Key` on each line is optional but strongly recommended for anything beyond a single request.
File-mode output order is not documented as guaranteed to match input order, so `Key` is the reliable
way to match a result back to the request that produced it (see `InlinedBatchResponse.Key` below).

### From a file you already built or uploaded

If you already have a JSONL file (built by an external pipeline, say) and just need to reference it:

```csharp
var uploaded = await fileService.UploadFileAsync(jsonlBytes, "jsonl", "my-requests.jsonl");
var job = await batchService.CreateFromFileAsync("gemini-3.6-flash", uploaded.Name!, "my-batch-job");
```

File-mode's ceiling is 2GB per file.

## Waiting for completion

```csharp
var finished = await batchService.WaitUntilCompleteAsync(
    job.Name!,
    pollInterval: TimeSpan.FromMinutes(1),
    timeout: TimeSpan.FromHours(2)); // no sensible default exists; pick one that fits your workload
```

This polls `GetAsync` until the job reaches a terminal state (succeeded, failed, cancelled, or
expired). Given the 24-hour target turnaround and 48-hour hard expiry, don't block a request/response
web request on this. Run it from a background job, a queue worker, or similar.

If you're polling manually instead, `BatchService.IsTerminalState(job.State)` is the same check
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

`GetResultsAsync` handles both destinations transparently. If the job's results are inline, they're
already on the `BatchJob` object and this returns them directly; if they're file-based, this downloads
and parses the JSONL output for you. Either way you get back the same `IReadOnlyList<InlinedBatchResponse>`.

A batch job succeeding as a whole doesn't mean every request in it succeeded. Check
`BatchJob.BatchStats.FailedRequestCount`, and check each result's `Error` individually the way the
loop above does. One bad request in a 10,000-request batch doesn't fail the other 9,999.

## Cancelling and cleaning up

```csharp
await batchService.CancelAsync(job.Name!);
await batchService.DeleteAsync(job.Name!);
```

Cancellation is asynchronous on Google's side too. A successful `CancelAsync` call means the request
was accepted, not that the job is cancelled the instant it returns. Call `GetAsync` afterward if you
need to observe the actual state transition.

Job results are retained for 6 weeks after completion, then permanently deleted by Google regardless
of whether you called `DeleteAsync` yourself.

## What this library does not do for you

- **Enforce Google's size/concurrency limits client-side.** The ~20MB inline ceiling, ~2GB file
  ceiling, and the documented 100-concurrent-jobs limit are all left to the API to reject; this library
  doesn't pre-validate any of them. You'll get a normal `GeminiApiException` if you go over.
- **Cost governance.** `ICostGovernor`'s daily budget and per-request estimate (see
  [Cost governance](cost-governance.md)) do not apply to batch jobs. Batch responses do carry real
  usage data once results come back, but at the discounted batch rate, which this library's pricing
  model doesn't yet represent. Tracking batch spend is a possible future addition, not something this
  release does.
- **Rate limiting.** Batch calls intentionally bypass `IRateLimiter` (which is tuned for interactive
  per-minute request limits); batch draws from a wholly separate quota pool on Google's side.
- **Batch embeddings, or batch image generation.** This release covers `generateContent` only.

## A note on the state string

`BatchJob.State` is a plain string, not a C# enum, because Google's own documentation is inconsistent
about the exact prefix (some pages/tools show `JOB_STATE_RUNNING`, `JOB_STATE_SUCCEEDED`, etc.; others
show `BATCH_STATE_RUNNING`). `BatchService.IsTerminalState` checks the *suffix* (`SUCCEEDED`,
`FAILED`, `CANCELLED`, `EXPIRED`) case-insensitively, so it works correctly regardless of which prefix
your project's API responses actually use. Don't compare `State` against a hardcoded literal like
`"JOB_STATE_SUCCEEDED"` in your own code for the same reason.
