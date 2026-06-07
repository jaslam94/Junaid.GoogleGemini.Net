using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

public class GeminiRateLimiterTests
{
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
}
