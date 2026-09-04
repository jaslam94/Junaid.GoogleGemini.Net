using BenchmarkDotNet.Attributes;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// The full <c>AddGemini</c> pipeline (auth handler, retry/resilience handler, rate limiter, cost
/// governor, telemetry) at its out-of-the-box defaults: no budget configured, and, realistically,
/// since most apps don't wire one up, no OpenTelemetry listener attached. This is what a typical
/// consumer's overhead actually looks like.
/// </summary>
/// <remarks>
/// See <see cref="BenchmarkHost"/> for the shared DI wiring, and its doc comment for why rate
/// limiting is disabled here specifically (isolating measured overhead from the limiter's
/// intentional throttling delay, not a claim that it's off by default).
/// </remarks>
[MemoryDiagnoser]
public class GeminiClientDefaultBenchmarks
{
    private ServiceProvider _provider = null!;
    private IGeminiService _service = null!;

    [GlobalSetup]
    public void Setup() => (_provider, _service) = BenchmarkHost.BuildGeminiService();

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    [Benchmark]
    public async Task<string> TextGeneration()
    {
        var response = await _service.GenerateAsync(BenchmarkFixtures.Text);
        return response.Text();
    }
}
