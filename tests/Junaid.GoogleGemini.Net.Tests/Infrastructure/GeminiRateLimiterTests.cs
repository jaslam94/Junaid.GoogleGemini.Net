using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

public class GeminiRateLimiterTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GeminiRateLimiter(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_EnabledWithNonPositiveRequestsPerMinute_ThrowsArgumentOutOfRangeException(
        int requestsPerMinute)
    {
        // Guards the below-60-RPM branch's `60.0 / requestsPerMinute` from ever seeing 0 or a negative
        // value, which would otherwise hand TimeSpan.FromSeconds a non-finite or negative value and
        // crash with an unrelated-looking exception instead of a clear one. Disabled=false with the
        // same value is fine (this branch never runs), which is exercised implicitly by
        // Disabled_AllowsAcquisition using the property default.
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeminiRateLimiter(
            new RateLimitOptions { Enabled = true, RequestsPerMinute = requestsPerMinute }));
    }

    [Fact]
    public async Task Disabled_AllowsAcquisition()
    {
        using var limiter = GeminiRateLimiter.CreateDisabled();

        using var lease = await limiter.AcquireAsync();

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task Enabled_AllowsInitialRequestWithinBudget()
    {
        using var limiter = new GeminiRateLimiter(
            new RateLimitOptions { Enabled = true, RequestsPerMinute = 60 });

        using var lease = await limiter.AcquireAsync();

        Assert.True(lease.IsAcquired);
    }

    // Regression test for a real bug: the bucket used to compute tokensPerSecond as
    // Math.Max(1, RequestsPerMinute / 60), which floors ANY RequestsPerMinute under 60 to an
    // effective 60 RPM — silently ignoring the configured value for exactly the low-RPM range
    // free/low-tier Gemini quotas live in. This asserts the small fixed burst allowance (2 tokens)
    // is honored and doesn't silently balloon into a 60-RPM-equivalent bucket.
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(59)]
    public async Task Enabled_BelowSixtyRpm_HonorsSmallBurstNotSixtyRpmFloor(int requestsPerMinute)
    {
        using var limiter = new GeminiRateLimiter(
            new RateLimitOptions { Enabled = true, RequestsPerMinute = requestsPerMinute });

        // The fixed burst allowance (TokenLimit = 2) for the below-60 branch should be immediately
        // available regardless of which sub-60 rate was configured...
        using var lease1 = await limiter.AcquireAsync();
        using var lease2 = await limiter.AcquireAsync();
        Assert.True(lease1.IsAcquired);
        Assert.True(lease2.IsAcquired);

        // ...but a third immediate acquire must NOT succeed instantly. The old bug (flooring to a
        // 60-RPM-equivalent bucket, replenishing 1 token every ~17ms) would let a burst of far more
        // than 2 requests through with no meaningful delay; the fix replenishes 1 token every
        // (60 / requestsPerMinute) seconds, i.e. at least ~1.02s even at the top of this range, so a
        // 200ms timeout can only succeed if the old floor bug is back.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await limiter.AcquireAsync(cts.Token));
    }
}
