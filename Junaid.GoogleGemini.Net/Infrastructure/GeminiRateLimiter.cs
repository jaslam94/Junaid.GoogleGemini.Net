using System.Threading.RateLimiting;
using Junaid.GoogleGemini.Net.Infrastructure.Options;

namespace Junaid.GoogleGemini.Net.Infrastructure
{
    /// <summary>
    /// Interface for rate limiting Gemini API requests
    /// </summary>
    public interface IRateLimiter : IDisposable
    {
        /// <summary>
        /// Asynchronously acquires permission to make an API request
        /// </summary>
        ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current available permits (optional, may not be supported by all implementations)
        /// </summary>
        int? GetAvailablePermits();
    }

    /// <summary>
    /// Rate limiter for Gemini API requests using a token bucket algorithm
    /// </summary>
    public class GeminiRateLimiter : IRateLimiter
    {
        private readonly TokenBucketRateLimiter _rateLimiter;
        private readonly bool _isEnabled;

        /// <summary>
        /// Initializes a new instance of the GeminiRateLimiter
        /// </summary>
        /// <param name="options">Rate limiting configuration options</param>
        public GeminiRateLimiter(RateLimitOptions options)
        {
            _isEnabled = options.Enabled;

            if (_isEnabled)
            {
                var tokensPerSecond = Math.Max(1, options.RequestsPerMinute / 60);
                var bucketOptions = new TokenBucketRateLimiterOptions
                {
                    TokenLimit = tokensPerSecond * 2, // Allow burst of 2x the per-second rate
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10, // Keep queue small to avoid long waits
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    TokensPerPeriod = tokensPerSecond,
                    AutoReplenishment = true
                };

                _rateLimiter = new TokenBucketRateLimiter(bucketOptions);
            }
            else
            {
                // Create a no-op rate limiter when disabled
                _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(1),
                    TokensPerPeriod = int.MaxValue,
                    AutoReplenishment = true
                });
            }
        }

        /// <summary>
        /// Creates a disabled rate limiter for testing or when rate limiting is not needed
        /// </summary>
        public static GeminiRateLimiter CreateDisabled()
        {
            return new GeminiRateLimiter(new RateLimitOptions { Enabled = false });
        }

        /// <inheritdoc/>
        public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken = default)
        {
            return _rateLimiter.AcquireAsync(1, cancellationToken);
        }

        /// <inheritdoc/>
        public int? GetAvailablePermits()
        {
            // TokenBucketRateLimiter doesn't expose available tokens in .NET 6/7/8
            // Return null to indicate this information is not available
            return _isEnabled ? null : int.MaxValue;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _rateLimiter?.Dispose();
        }
    }
}