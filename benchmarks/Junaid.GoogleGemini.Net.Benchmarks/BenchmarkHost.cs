using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// The DI wiring shared by <see cref="GeminiClientDefaultBenchmarks"/> and
/// <see cref="GeminiClientFullyObservedBenchmarks"/>: the real <c>AddGemini</c> pipeline pointed at
/// an in-memory <see cref="FakeGeminiHandler"/> instead of the network. Factored out after three
/// independent review passes converged on the same finding -- this ~20-line sequence was
/// duplicated verbatim between the two classes, differing only in whatever budget/observability
/// config each one layers on top.
/// </summary>
internal static class BenchmarkHost
{
    /// <param name="configureExtra">
    /// Invoked after the shared baseline options (fake key/base URL, rate limiting off) are set, so
    /// a caller can add its own delta -- e.g. <see cref="GeminiClientFullyObservedBenchmarks"/>'s
    /// <c>Budget</c> block -- without repeating the baseline.
    /// </param>
    public static (ServiceProvider Provider, IGeminiService Service) BuildGeminiService(Action<GeminiOptions>? configureExtra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(); // no providers registered => near-zero-cost no-op logging
        services.AddGemini(options =>
        {
            options.ApiKey = "benchmark-key";
            options.BaseUrl = new Uri(BenchmarkFixtures.BaseUrl);
            options.RateLimit.Enabled = false; // isolate CPU/allocation overhead from the rate
                                                // limiter's intentional throttling delay -- a
                                                // benchmark loop fires thousands of calls/second,
                                                // which the default 60 RPM limiter would correctly
                                                // start queuing almost immediately, and a benchmark
                                                // that measured that would just be timing
                                                // Task.Delay, not the library's code. With headroom
                                                // under your configured RPM (the normal case, since
                                                // you're not calling in a tight loop), the token
                                                // bucket's fast path costs the same either way, so
                                                // this doesn't change what's measured.
            configureExtra?.Invoke(options);
        });

        // Swap the real transport for the in-memory fake, after AddGemini has already wired up
        // auth + resilience as DelegatingHandlers around it. Re-opening the same named
        // IHttpClientBuilder like this only replaces the primary handler; the handler chain
        // AddGemini already configured is untouched, so callers still pay the full cost of every
        // layer, they just never touch a real socket.
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => new FakeGeminiHandler());

        // Assign to a local before resolving from it: if GetRequiredService throws (e.g. options
        // validation), the provider must already be reachable to dispose here rather than leaked
        // in a variable about to go out of scope. BenchmarkDotNet isolates each benchmark class in
        // its own process (see docs/articles/benchmarks.md's Methodology section), so today a
        // GlobalSetup failure gets cleaned up by process exit either way -- this only matters if
        // that execution model ever changes (e.g. an in-process toolchain) -- but centralizing it
        // here means there's exactly one place doing this bookkeeping instead of one per caller.
        var provider = services.BuildServiceProvider();
        try
        {
            var service = provider.GetRequiredService<IGeminiService>();
            return (provider, service);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }
}
