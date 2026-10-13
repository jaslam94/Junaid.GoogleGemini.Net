# Resilience & rate limiting

Both are configured once and applied automatically to every call.

```csharp
builder.Services.AddGemini(options =>
{
    options.MaxRetries = 3;                          // retried on 429/5xx/transient faults
    options.RetryBaseDelay = TimeSpan.FromSeconds(2); // exponential backoff base
    options.RateLimit.Enabled = true;                // client-side token bucket
    options.RateLimit.RequestsPerMinute = 60;
});
```

## Retries

On net8.0 and net9.0, retries run on the `HttpClient` pipeline via
`Microsoft.Extensions.Http.Resilience` (Polly v8): exponential backoff with jitter, retrying HTTP
429, 5xx, `HttpRequestException`, and timeouts.

On netstandard2.0, that package is not available. The library uses an equivalent built-in
`GeminiRetryHandler` instead.

Either way, retries happen inside a single logical send against buffered content. A request is
never half-sent and corrupted. Earlier hand-rolled retry code had that bug.

## Client-side rate limiting

A token-bucket limiter (built on `System.Threading.RateLimiting`) caps outgoing requests before they
leave your process, smoothing bursts and helping you stay under quota. When the limiter rejects a
call you get a `GeminiRateLimitException`. Disable it with `RateLimit.Enabled = false`.

## Timeouts

`TimeoutSeconds` bounds each request; on expiry you get a `GeminiTimeoutException`. Genuine caller
cancellation (your `CancellationToken`) propagates as `OperationCanceledException` instead.

## Batch API is the one exception

`IBatchService` calls get the same retry handling described above, but they bypass the
client-side rate limiter. Batch draws from a separate quota pool on Google's side. A limiter tuned
for interactive per-minute requests would be wrong for it. See [Batch API](batch-api.md) for details.
