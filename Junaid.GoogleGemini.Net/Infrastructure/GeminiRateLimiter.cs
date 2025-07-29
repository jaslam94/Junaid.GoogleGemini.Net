using System.Threading.RateLimiting;

namespace Junaid.GoogleGemini.Net.Infrastructure;

/// <summary>
/// Rate limiter for Gemini API requests using a token bucket algorithm.
/// </summary>
public class GeminiRateLimiter
{
    private readonly TokenBucketRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiRateLimiter"/> class.
    /// </summary>
    /// <param name="tokensPerSecond">Number of allowed requests per second (default is 60).</param>
    public GeminiRateLimiter(int tokensPerSecond = 60)
    {
        var options = new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokensPerSecond,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 100,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = tokensPerSecond,
            AutoReplenishment = true
        };

        _rateLimiter = new TokenBucketRateLimiter(options);
    }

    /// <summary>
    /// Asynchronously acquires permission to make an API request.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="RateLimitLease"/> indicating whether the request was permitted.</returns>
    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        return _rateLimiter.AcquireAsync(1, cancellationToken);
    }

    /// <summary>
    /// Gets the current number of available tokens in the bucket.
    /// </summary>
    /// <returns>The number of tokens available for immediate use.</returns>
    public int GetAvailableTokens()
    {
        return _rateLimiter.GetAvailableTokens();
    }

    /// <summary>
    /// Disposes the rate limiter.
    /// </summary>
    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}