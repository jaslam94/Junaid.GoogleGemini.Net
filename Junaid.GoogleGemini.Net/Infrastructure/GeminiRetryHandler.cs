namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>
    /// A minimal retry <see cref="DelegatingHandler"/> used on <c>netstandard2.0</c>, where
    /// Microsoft.Extensions.Http.Resilience isn't available. Retries transient failures (HTTP 429,
    /// 5xx, and <see cref="HttpRequestException"/>) with exponential backoff.
    /// </summary>
    /// <remarks>
    /// Re-sending the same request is safe here because the client always uses buffered content
    /// (<see cref="StringContent"/>/<see cref="ByteArrayContent"/>): inner handlers re-read it each
    /// attempt, and disposal only happens once the outer <see cref="HttpClient"/> call completes.
    /// On net8+ the standard resilience handler is used instead (see GeminiExtensions).
    /// </remarks>
    internal sealed class GeminiRetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;

        public GeminiRetryHandler(int maxRetries, TimeSpan baseDelay)
        {
            _maxRetries = Math.Max(0, maxRetries);
            _baseDelay = baseDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : baseDelay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (attempt >= _maxRetries || !IsTransient(response.StatusCode))
                    {
                        return response;
                    }
                    response.Dispose(); // discard the transient response before retrying
                }
                catch (HttpRequestException) when (attempt < _maxRetries)
                {
                    // fall through to backoff and retry
                }

                var delayMs = _baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
            }
        }

        private static bool IsTransient(System.Net.HttpStatusCode statusCode)
        {
            var code = (int)statusCode;
            return code == 429 || code >= 500;
        }
    }
}
