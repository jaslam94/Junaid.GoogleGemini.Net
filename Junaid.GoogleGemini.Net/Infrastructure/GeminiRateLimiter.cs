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
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="options"/>.<see cref="RateLimitOptions.RequestsPerMinute"/> is less than 1
        /// while <see cref="RateLimitOptions.Enabled"/> is true. This constructor is public and doesn't
        /// go through <c>GeminiOptionsValidator</c> (which enforces the same [1, 1000] range when
        /// options are bound via DI), so it re-checks here: without this guard, RequestsPerMinute &lt;=
        /// 0 would flow into <c>60.0 / RequestsPerMinute</c> and hand <c>TimeSpan.FromSeconds</c> a
        /// non-finite or negative value, crashing with an unrelated-looking overflow/argument exception
        /// instead of a clear one.
        /// </exception>
        public GeminiRateLimiter(RateLimitOptions options)
        {
            // `?? throw`, not ArgumentNullException.ThrowIfNull: this library multi-targets
            // netstandard2.0 (see PolySharp usage elsewhere), and ThrowIfNull is a .NET 6+ addition to
            // an external BCL type that PolySharp can't shim (it can polyfill compiler-recognized
            // attributes/markers, not add new static members to a sealed system type). Matches the
            // pattern already used by every other constructor in this codebase, e.g. GeminiClient's.
            _ = options ?? throw new ArgumentNullException(nameof(options));

            _isEnabled = options.Enabled;

            if (_isEnabled)
            {
                if (options.RequestsPerMinute < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(options),
                        options.RequestsPerMinute,
                        "RequestsPerMinute must be at least 1 when rate limiting is enabled.");
                }

                TokenBucketRateLimiterOptions bucketOptions;

                if (options.RequestsPerMinute >= 60)
                {
                    var tokensPerSecond = options.RequestsPerMinute / 60;
                    bucketOptions = new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = tokensPerSecond * 2, // Allow burst of 2x the per-second rate
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10, // Keep queue small to avoid long waits
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        TokensPerPeriod = tokensPerSecond,
                        AutoReplenishment = true
                    };
                }
                else
                {
                    // Below 60 RPM, a 1-second replenishment period can only express whole
                    // tokens/second, which used to floor via Math.Max(1, RequestsPerMinute / 60). That means
                    // ANY configured rate under 60 silently became 60 RPM regardless of what was asked
                    // for. That's a real bug: this is exactly the range free/low-tier Gemini quotas
                    // live in, so it defeated the limiter for the callers who most needed it. Fix:
                    // replenish exactly 1 token every (60 / RequestsPerMinute) seconds instead, which
                    // reproduces the configured rate exactly (fractional seconds are valid for
                    // TimeSpan/ReplenishmentPeriod) while still trickling smoothly rather than
                    // bursting once a minute.
                    var secondsPerToken = 60.0 / options.RequestsPerMinute;
                    bucketOptions = new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 2, // small burst allowance, same shape as the >=60 branch
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(secondsPerToken),
                        TokensPerPeriod = 1,
                        AutoReplenishment = true
                    };
                }

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