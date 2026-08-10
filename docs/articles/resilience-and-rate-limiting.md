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

On **net8.0/net9.0** retries run on the `HttpClient` pipeline via
`Microsoft.Extensions.Http.Resilience` (Polly v8): exponential backoff with jitter, retrying HTTP
429, 5xx, `HttpRequestException`, and timeouts.

On **netstandard2.0** (where that package isn't available) an equivalent built-in
`GeminiRetryHandler` is used instead.

Either way, retries happen *inside* a single logical send against buffered content, so a request is
never corrupted by being half-sent, which was the bug that plagued earlier hand-rolled retry code.

## Client-side rate limiting

A token-bucket limiter (built on `System.Threading.RateLimiting`) caps outgoing requests before they
leave your process, smoothing bursts and helping you stay under quota. When the limiter rejects a
call you get a `GeminiRateLimitException`. Disable it with `RateLimit.Enabled = false`.

## Timeouts

`TimeoutSeconds` bounds each request; on expiry you get a `GeminiTimeoutException`. Genuine caller
cancellation (your `CancellationToken`) propagates as `OperationCanceledException` instead.
