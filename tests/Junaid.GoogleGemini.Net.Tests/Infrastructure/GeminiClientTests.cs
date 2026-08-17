using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

public class GeminiClientTests
{
    private static GeminiClient CreateClient(FakeHttpMessageHandler handler, ICostGovernor? costGovernor = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        return new GeminiClient(
            httpClient,
            NullLogger<GeminiClient>.Instance,
            GeminiRateLimiter.CreateDisabled(),
            costGovernor ?? GeminiCostGovernor.CreateDisabled());
    }

    [Fact]
    public async Task PostAsync_WhenApiReturnsCandidates_DeserializesText()
    {
        const string json = """
        {"candidates":[{"content":{"role":"model","parts":[{"text":"Hello!"}]},"finishReason":"STOP","index":0}]}
        """;
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var response = await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-2.5-pro:generateContent", new GenerateContentRequest());

        Assert.Equal("Hello!", response.Text());
        Assert.Single(handler.Requests);
        Assert.True(handler.Requests[0].Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task PostAsync_WhenApiReturnsError_ThrowsGeminiExceptionWithStatus()
    {
        const string json = """{"error":{"code":400,"message":"Invalid request","status":"INVALID_ARGUMENT"}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.BadRequest, json);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("INVALID_ARGUMENT", ex.Status);
        Assert.Contains("Invalid request", ex.Message);
        // A 400 is a client error and must NOT be retried.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostAsync_WhenSuccessBodyIsInvalidJson_ThrowsSerializationException()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "this is not json");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<GeminiSerializationException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));
    }

    [Fact]
    public async Task PostAsync_WhenSendTimesOut_ThrowsTimeoutException()
    {
        // A timeout surfaces as a TaskCanceledException while the caller's token is NOT cancelled.
        var handler = new FakeHttpMessageHandler((_, _, _) =>
            throw new TaskCanceledException("simulated timeout"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<GeminiTimeoutException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));
    }

    [Fact]
    public async Task StreamAsync_ParsesSseEventsIntoChunks()
    {
        const string sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hello \"}]}}]}\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"world\"}]}}]}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var client = CreateClient(handler);

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamAsync(
            "models/x:streamGenerateContent?alt=sse", new GenerateContentRequest()))
        {
            chunks.Add(chunk.Text());
        }

        Assert.Equal(["Hello ", "world"], chunks);
    }

    [Fact]
    public async Task StreamAsync_SkipsMalformedEvents()
    {
        const string sse =
            "data: this-is-not-json\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var client = CreateClient(handler);

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamAsync(
            "models/x:streamGenerateContent?alt=sse", new GenerateContentRequest()))
        {
            chunks.Add(chunk.Text());
        }

        Assert.Equal(["ok"], chunks);
    }

    [Fact]
    public async Task PostAsync_EmitsActivity_WithGenAiTags()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GeminiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            // Filter here, not just in the final assertion: ActivitySource/ActivityListener are
            // process-global, so with xUnit's default test parallelism this listener also observes
            // activities from other test classes racing on the same source concurrently (including
            // StreamAsync's, now that it's instrumented too). Filtering at collection time keeps
            // `activities` scoped to this test's own call; filtering only in Assert.Single left a gap
            // where a same-run cross-talk activity could be the sole item collected, failing the
            // predicate with a confusing "found one, but it's not mine" message.
            ActivityStopped = a =>
            {
                if (a.DisplayName == "generateContent gemini-activity-probe") activities.Add(a);
            },
        };
        ActivitySource.AddActivityListener(listener);

        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":2,"totalTokenCount":7}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        // A unique model name keeps this assertion isolated from activities that other test classes
        // emit in parallel onto the same (process-global) ActivitySource.
        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-activity-probe:generateContent", new GenerateContentRequest());

        var activity = Assert.Single(activities);
        Assert.Equal("gemini", activity.GetTagItem("gen_ai.system"));
        Assert.Equal("gemini-activity-probe", activity.GetTagItem("gen_ai.request.model"));
        Assert.Equal(5, activity.GetTagItem("gen_ai.usage.input_tokens"));
    }

    [Fact]
    public async Task PostAsync_WhenMeterListenerAttached_RecordsTokenCostAndDurationMetrics()
    {
        // Before this test, GeminiTelemetry.RecordUsage/RecordCost/RecordDuration's
        // Instrument<T>.Enabled guards (added alongside this test) had zero coverage anywhere in
        // the suite -- no other test ever attaches a MeterListener, only ActivityListener. A bug
        // in this exact logic (e.g. the guard condition inverted, or the input/output token tags
        // swapped) would previously have shipped with a fully green build.
        var tokenMeasurements = new List<(long Value, string? TokenType)>();
        double? durationSeconds = null;
        double? costUsd = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == GeminiTelemetry.SourceName) l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != "gen_ai.client.token.usage") return;
            if (!HasTag(tags, "gen_ai.request.model", "gemini-metrics-probe")) return; // isolate from parallel tests
            tokenMeasurements.Add((measurement, GetTag(tags, "gen_ai.token.type")));
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (!HasTag(tags, "gen_ai.request.model", "gemini-metrics-probe")) return;
            switch (instrument.Name)
            {
                case "gen_ai.client.operation.duration": durationSeconds = measurement; break;
                case "gemini.client.cost.usd": costUsd = measurement; break;
            }
        });
        listener.Start();

        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":2,"totalTokenCount":7}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);

        // GeminiTelemetry.RecordCost is internal with no InternalsVisibleTo into this project, and
        // reaching it requires ICostGovernor's recordCost callback to actually be wired to it --
        // that wiring only happens inside GeminiExtensions.AddGemini's DI registration, so (unlike
        // the rest of this file's tests, which construct GeminiClient directly) this one has to go
        // through the real DI pipeline, the same way GeminiResilienceTests does. A unique probe
        // model name (rather than a real priced model like "gemini-3.7-flash") both isolates this
        // test from parallel cross-talk on the process-global Meter, the same reasoning the
        // Activity test above uses, and needs its own pricing override since it isn't in the
        // built-in table.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.RateLimit.Enabled = false;
            o.Budget = new BudgetOptions
            {
                Enabled = false,
                ModelPricingOverrides = new Dictionary<string, ModelPricing>
                {
                    ["gemini-metrics-probe"] = new() { InputPerMillionTokensUsd = 1m, OutputPerMillionTokensUsd = 1m, CachedInputPerMillionTokensUsd = 1m },
                },
            };
        });
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        var client = services.BuildServiceProvider().GetRequiredService<IGeminiClient>();

        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-metrics-probe:generateContent", new GenerateContentRequest());

        Assert.Contains(tokenMeasurements, m => m.TokenType == "input" && m.Value == 5);
        Assert.Contains(tokenMeasurements, m => m.TokenType == "output" && m.Value == 2);
        Assert.NotNull(durationSeconds);
        Assert.True(durationSeconds >= 0);
        Assert.NotNull(costUsd);
        Assert.True(costUsd > 0);
    }

    private static bool HasTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key, string value)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key) return Equals(tag.Value, value);
        }
        return false;
    }

    private static string? GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key) return tag.Value as string;
        }
        return null;
    }

    [Fact]
    public async Task PostAsync_WhenBudgetExceeded_ThrowsUnwrapped_AndNeverSendsTheRequest()
    {
        const string json = """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]}}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var governor = new FakeCostGovernor { ThrowOnCheckBudget = true };
        var client = CreateClient(handler, governor);

        await Assert.ThrowsAsync<GeminiBudgetExceededException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));

        // The rejected call never reaches the network.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PostAsync_OnSuccess_RecordsSpendWithTheResponsesRealUsage()
    {
        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":4,"totalTokenCount":14}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var governor = new FakeCostGovernor();
        var client = CreateClient(handler, governor);

        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-2.5-flash:generateContent", new GenerateContentRequest());

        var (model, usage) = Assert.Single(governor.RecordSpendCalls);
        Assert.Equal("gemini-2.5-flash", model);
        Assert.Equal(10, usage.PromptTokenCount);
        Assert.Equal(4, usage.CandidatesTokenCount);
    }

    [Fact]
    public async Task StreamAsync_RecordsSpendOnce_UsingTheFinalChunksUsage()
    {
        const string sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hello \"}]}}]}\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"world\"}]}}],\"usageMetadata\":{\"promptTokenCount\":3,\"candidatesTokenCount\":2,\"totalTokenCount\":5}}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var governor = new FakeCostGovernor();
        var client = CreateClient(handler, governor);

        await foreach (var _ in client.StreamAsync(
            "models/gemini-2.5-flash:streamGenerateContent?alt=sse", new GenerateContentRequest()))
        {
            // drain
        }

        var (model, usage) = Assert.Single(governor.RecordSpendCalls);
        Assert.Equal("gemini-2.5-flash", model);
        Assert.Equal(3, usage.PromptTokenCount);
        Assert.Equal(2, usage.CandidatesTokenCount);
    }

    [Fact]
    public async Task StreamAsync_WhenCancelledBeforeFinalChunk_DoesNotRecordSpend()
    {
        const string sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hello \"}]}}]}\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"world\"}]}}],\"usageMetadata\":{\"promptTokenCount\":3,\"candidatesTokenCount\":2,\"totalTokenCount\":5}}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var governor = new FakeCostGovernor();
        var client = CreateClient(handler, governor);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in client.StreamAsync(
                "models/gemini-2.5-flash:streamGenerateContent?alt=sse", new GenerateContentRequest(), cts.Token))
            {
                // Cancel as soon as the first (non-final) chunk arrives, before the final
                // usage-carrying chunk is ever read.
                cts.Cancel();
            }
        });

        Assert.Empty(governor.RecordSpendCalls);
    }

    private sealed class FakeCostGovernor : ICostGovernor
    {
        public bool ThrowOnCheckBudget { get; set; }
        public List<(string? Model, UsageMetadata Usage)> RecordSpendCalls { get; } = [];

        public void CheckBudget()
        {
            if (ThrowOnCheckBudget)
            {
                throw new GeminiBudgetExceededException("budget exceeded", currentSpendUsd: 1m, budgetLimitUsd: 1m);
            }
        }

        public decimal RecordSpend(string? model, UsageMetadata usage)
        {
            RecordSpendCalls.Add((model, usage));
            return 0m;
        }

        public decimal GetTodaySpend() => 0m;

        public bool HasRequestCeiling => false;

        public decimal CheckEstimatedRequestCost(string? model, int inputTokens, int? maxOutputTokens) => 0m;
    }
}
