using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Junaid.GoogleGemini.Net.Benchmarks;

/// <summary>
/// The same pipeline as <see cref="GeminiClientDefaultBenchmarks"/>, but with every optional
/// feature actually turned on and actively observed: a daily cost budget (so
/// <c>ICostGovernor.CheckBudget</c>/<c>RecordSpend</c> run their real logic instead of the
/// zero-overhead "not configured" short-circuit) and a subscribed <see cref="ActivityListener"/> +
/// <see cref="MeterListener"/> (so every span/metric this library emits is actually recorded, not
/// dropped as a no-op because nobody's listening). This is the realistic worst case: a production
/// app with cost governance and OpenTelemetry both wired up.
/// </summary>
[MemoryDiagnoser]
public class GeminiClientFullyObservedBenchmarks
{
    private ServiceProvider _provider = null!;
    private IGeminiService _service = null!;
    private ActivityListener _activityListener = null!;
    private MeterListener _meterListener = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Subscribe to this library's ActivitySource/Meter exactly the way the README's
        // AddOpenTelemetry() snippet does, so StartOperation/RecordUsage/RecordCost/RecordDuration
        // all take their "someone is listening" path instead of the no-op fast path.
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GeminiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == GeminiTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        // Everything from here on can throw (options validation inside BuildServiceProvider,
        // GetRequiredService resolving it) -- if it does, BenchmarkDotNet won't call Cleanup()
        // below (GlobalSetup failing is fatal), so clean up whatever was already registered/built
        // ourselves rather than leaking the listeners (and, if it got that far, the provider).
        try
        {
            ActivitySource.AddActivityListener(_activityListener);
            _meterListener.Start();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddGemini(options =>
            {
                options.ApiKey = "benchmark-key";
                options.BaseUrl = new Uri("https://benchmark.invalid/v1beta/");
                options.RateLimit.Enabled = false; // isolate CPU/allocation overhead — see the sibling
                                                    // benchmark class's remarks for why this doesn't
                                                    // change the per-call cost being measured.
                options.Budget = new BudgetOptions
                {
                    Enabled = true,
                    MaxCostPerDayUsd = 1_000_000m, // effectively never reached, so CheckBudget's real
                                                    // comparison runs without ever throwing mid-benchmark
                };
            });

            services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => new FakeGeminiHandler());

            _provider = services.BuildServiceProvider();
            _service = _provider.GetRequiredService<IGeminiService>();
        }
        catch
        {
            _provider?.Dispose();
            _meterListener.Dispose();
            _activityListener.Dispose();
            throw;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    [Benchmark]
    public async Task<string> TextGeneration()
    {
        var response = await _service.GenerateAsync(BenchmarkPrompt.Text);
        return response.Text();
    }
}
