using BenchmarkDotNet.Attributes;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// The full <c>AddGemini</c> pipeline (auth handler, retry/resilience handler, rate limiter, cost
/// governor, telemetry) at its out-of-the-box defaults: no budget configured, and — realistically,
/// since most apps don't wire one up — no OpenTelemetry listener attached. This is what a typical
/// consumer's overhead actually looks like.
/// </summary>
/// <remarks>
/// Rate limiting is turned off here specifically, not because it's disabled by default (it isn't —
/// <see cref="Infrastructure.Options.RateLimitOptions.Enabled"/> defaults to <c>true</c>), but
/// because a benchmark loop issues thousands of calls/second, which the default 60-requests/minute
/// limiter would correctly start queuing after the first couple of calls. That queuing delay is
/// the rate limiter doing its job, not overhead — measuring it here would just be timing
/// <c>Task.Delay</c>. With headroom under the configured RPM (the normal case for a real app,
/// which isn't calling in a tight loop), <c>TokenBucketRateLimiter.AcquireAsync</c> takes the same
/// fast synchronous-lease path this benchmark exercises either way, so disabling it changes nothing
/// about the number reported here — see <see cref="GeminiClientFullyObservedBenchmarks"/> for the
/// same reasoning applied to cost governance and telemetry.
/// </remarks>
[MemoryDiagnoser]
public class GeminiClientDefaultBenchmarks
{
    private ServiceProvider _provider = null!;
    private IGeminiService _service = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // no providers registered => near-zero-cost no-op logging
        services.AddGemini(options =>
        {
            options.ApiKey = "benchmark-key";
            options.BaseUrl = new Uri("https://benchmark.invalid/v1beta/");
            options.RateLimit.Enabled = false; // see remarks above
        });

        // Swap the real transport for the in-memory fake, after AddGemini has already wired up
        // auth + resilience as DelegatingHandlers around it. Re-opening the same named
        // IHttpClientBuilder like this only replaces the primary handler; the handler chain
        // AddGemini already configured is untouched, so this benchmark still pays the full cost of
        // every layer, it just never touches a real socket.
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => new FakeGeminiHandler());

        _provider = services.BuildServiceProvider();
        _service = _provider.GetRequiredService<IGeminiService>();
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark]
    public async Task<string> TextGeneration()
    {
        var response = await _service.GenerateAsync(BenchmarkPrompt.Text);
        return response.Text();
    }
}
