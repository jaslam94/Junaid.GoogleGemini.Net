using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

public class GeminiCostGovernorTests
{
    // Clean, easy-to-hand-verify rates: 1 token = exactly $0.000001 at the "PerMillion" numbers below,
    // scaled per test so expected totals come out to round numbers.
    private const string TestModel = "test-model";

    private static ModelPricing MakePricing(
        decimal inputPerMillion, decimal outputPerMillion, decimal cachedPerMillion,
        HighVolumeTier? highVolumeTier = null) => new()
    {
        InputPerMillionTokensUsd = inputPerMillion,
        OutputPerMillionTokensUsd = outputPerMillion,
        CachedInputPerMillionTokensUsd = cachedPerMillion,
        HighVolumeTier = highVolumeTier,
    };

    private static GeminiCostGovernor CreateGovernor(
        ModelPricing? pricing,
        List<(string? Model, decimal CostUsd)>? recorded = null,
        decimal? maxCostPerDayUsd = null,
        decimal? maxCostPerRequestUsd = null,
        bool enabled = true,
        Func<DateTime>? clock = null)
    {
        recorded ??= [];
        var options = new BudgetOptions
        {
            Enabled = enabled,
            MaxCostPerDayUsd = maxCostPerDayUsd,
            MaxCostPerRequestUsd = maxCostPerRequestUsd,
            ModelPricingOverrides = pricing is null
                ? null
                : new Dictionary<string, ModelPricing> { [TestModel] = pricing },
        };
        return new GeminiCostGovernor(options, (model, cost) => recorded.Add((model, cost)), utcNowProvider: clock);
    }

    [Fact]
    public void RecordSpend_PlainCall_NoCachingNoThinking_ComputesExactCost()
    {
        var pricing = MakePricing(inputPerMillion: 1.00m, outputPerMillion: 2.00m, cachedPerMillion: 0.50m);
        var governor = CreateGovernor(pricing);

        var cost = governor.RecordSpend(TestModel, new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CandidatesTokenCount = 1_000_000,
        });

        // inputCost = 1M/1M * 1.00 = 1.00; outputCost = 1M/1M * 2.00 = 2.00
        Assert.Equal(3.00m, cost);
    }

    [Fact]
    public void RecordSpend_ThoughtsTokens_AreBilledAsOutput_NotInput()
    {
        var pricing = MakePricing(inputPerMillion: 1.00m, outputPerMillion: 2.00m, cachedPerMillion: 0.50m);

        var withThoughts = CreateGovernor(pricing).RecordSpend(TestModel, new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CandidatesTokenCount = 500_000,
            ThoughtsTokenCount = 500_000,
        });

        var withoutThoughts = CreateGovernor(pricing).RecordSpend(TestModel, new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CandidatesTokenCount = 500_000,
        });

        // With thoughts: inputCost 1.00 + outputCost (500k+500k)/1M*2.00 = 1.00 + 2.00 = 3.00
        Assert.Equal(3.00m, withThoughts);
        // Without: inputCost 1.00 + outputCost 500k/1M*2.00 = 1.00 + 1.00 = 2.00
        Assert.Equal(2.00m, withoutThoughts);
        // A naive implementation that ignored ThoughtsTokenCount entirely, or billed it as input,
        // would not show this $1.00 difference.
        Assert.NotEqual(withoutThoughts, withThoughts);
    }

    [Fact]
    public void RecordSpend_CachedTokens_AreDiscounted_AndNotDoubleCharged()
    {
        var pricing = MakePricing(inputPerMillion: 1.00m, outputPerMillion: 0m, cachedPerMillion: 0.50m);
        var governor = CreateGovernor(pricing);

        var cost = governor.RecordSpend(TestModel, new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CachedContentTokenCount = 400_000,
        });

        // billableInput = 1,000,000 - 400,000 = 600,000 -> 0.60
        // cachedCost = 400,000/1,000,000 * 0.50 = 0.20
        // total = 0.80 (NOT 1.00 [full prompt, cache discount ignored] and NOT 1.20 [double-charged])
        Assert.Equal(0.80m, cost);
    }

    [Fact]
    public void RecordSpend_HighVolumeTier_AppliesOnlyStrictlyAboveThreshold()
    {
        var pricing = MakePricing(
            inputPerMillion: 1.00m, outputPerMillion: 0m, cachedPerMillion: 0m,
            highVolumeTier: new HighVolumeTier
            {
                ThresholdTokens = 100,
                InputPerMillionTokensUsd = 10.00m,
                OutputPerMillionTokensUsd = 0m,
                CachedInputPerMillionTokensUsd = 0m,
            });

        var atThreshold = CreateGovernor(pricing).RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 100 });
        var aboveThreshold = CreateGovernor(pricing).RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 101 });

        // At exactly the threshold: base rate (100/1e6 * 1.00).
        Assert.Equal(100m / 1_000_000m * 1.00m, atThreshold);
        // One token above: the high-volume rate applies to the WHOLE call (101/1e6 * 10.00), not just
        // the excess — this mirrors how the plan's formula selects a single tier per call.
        Assert.Equal(101m / 1_000_000m * 10.00m, aboveThreshold);
        Assert.True(aboveThreshold > atThreshold * 10, "crossing the threshold should jump to the high-volume rate");
    }

    [Fact]
    public void CheckBudget_ThrowsExactlyAtTheCeiling_NotOneCentBelow()
    {
        // 1 token = $0.01 at this rate, for clean cent-level boundary math.
        var pricing = MakePricing(inputPerMillion: 10_000m, outputPerMillion: 0m, cachedPerMillion: 0m);
        var governor = CreateGovernor(pricing, maxCostPerDayUsd: 5.00m);

        governor.RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 499 }); // $4.99
        var exBelow = Record.Exception(() => governor.CheckBudget());
        Assert.Null(exBelow);

        governor.RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 1 }); // +$0.01 = $5.00
        var thrown = Assert.Throws<GeminiBudgetExceededException>(() => governor.CheckBudget());
        Assert.Equal(5.00m, thrown.CurrentSpendUsd);
        Assert.Equal(5.00m, thrown.BudgetLimitUsd);
    }

    [Fact]
    public void CreateDisabled_NeverThrows_ButStillRecordsSpend()
    {
        var governor = GeminiCostGovernor.CreateDisabled();

        var cost = governor.RecordSpend("gemini-2.5-flash", new UsageMetadata
        {
            PromptTokenCount = 1_000_000_000,
            CandidatesTokenCount = 1_000_000_000,
        });

        Assert.True(cost > 0m, "observability (cost computation) must stay on even when enforcement is disabled");
        var ex = Record.Exception(() => governor.CheckBudget());
        Assert.Null(ex); // enforcement never triggers, regardless of how much was recorded
    }

    [Fact]
    public void NullBudget_IsZeroOverheadNoOp()
    {
        var recorded = new List<(string?, decimal)>();
        var governor = new GeminiCostGovernor(
            options: null, recordCost: (m, c) => recorded.Add((m, c)));

        var cost = governor.RecordSpend("gemini-2.5-flash", new UsageMetadata
        {
            PromptTokenCount = 1_000_000,
            CandidatesTokenCount = 1_000_000,
        });

        Assert.Equal(0m, cost);
        Assert.Empty(recorded); // never even computed/emitted the metric
        Assert.Equal(0m, governor.GetTodaySpend());
        var ex = Record.Exception(() => governor.CheckBudget());
        Assert.Null(ex);
    }

    [Fact]
    public void DayRollover_YesterdaysSpend_DoesNotCountTowardTodaysCeiling()
    {
        var pricing = MakePricing(inputPerMillion: 10_000m, outputPerMillion: 0m, cachedPerMillion: 0m); // 1 token = $0.01
        var now = new DateTime(2026, 8, 8, 23, 0, 0, DateTimeKind.Utc);
        var governor = CreateGovernor(pricing, maxCostPerDayUsd: 1.00m, clock: () => now);

        // $2.00 of spend "yesterday" — well over the $1.00 ceiling, if it were to carry over.
        governor.RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 200 });

        now = now.AddDays(1); // roll into a new UTC calendar day

        Assert.Equal(0m, governor.GetTodaySpend());
        var ex = Record.Exception(() => governor.CheckBudget());
        Assert.Null(ex);
    }

    [Fact]
    public async Task RecordSpend_ConcurrentCalls_AllLand_NoLostUpdates()
    {
        var pricing = MakePricing(inputPerMillion: 1_000_000m, outputPerMillion: 0m, cachedPerMillion: 0m); // 1 token = $1
        var governor = CreateGovernor(pricing);

        var tasks = Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => governor.RecordSpend(TestModel, new UsageMetadata { PromptTokenCount = 1 })));
        await Task.WhenAll(tasks);

        Assert.Equal(200m, governor.GetTodaySpend());
    }

    [Fact]
    public void RecordSpend_UnknownModel_SkipsCostRecording_AndWarnsOncePerModel()
    {
        var logger = new CountingLogger();
        var recorded = new List<(string?, decimal)>();
        var governor = new GeminiCostGovernor(
            new BudgetOptions(), (model, cost) => recorded.Add((model, cost)), logger);

        var cost1 = governor.RecordSpend("some-unpriced-model", new UsageMetadata { PromptTokenCount = 100, CandidatesTokenCount = 50 });
        var cost2 = governor.RecordSpend("some-unpriced-model", new UsageMetadata { PromptTokenCount = 100, CandidatesTokenCount = 50 });

        Assert.Equal(0m, cost1);
        Assert.Equal(0m, cost2);
        Assert.Empty(recorded); // never assumes $0 by recording it — just skips recording entirely
        Assert.Equal(1, logger.WarningCount); // warned once, not once per call
    }

    // --- CheckEstimatedRequestCost / HasRequestCeiling (BudgetOptions.MaxCostPerRequestUsd) ---

    [Fact]
    public void HasRequestCeiling_TrueOnlyWhenEnabledAndCeilingConfigured()
    {
        Assert.True(CreateGovernor(pricing: null, maxCostPerRequestUsd: 1.00m).HasRequestCeiling);
        Assert.False(CreateGovernor(pricing: null, maxCostPerRequestUsd: null).HasRequestCeiling); // no ceiling set
        Assert.False(CreateGovernor(pricing: null, maxCostPerRequestUsd: 1.00m, enabled: false).HasRequestCeiling); // disabled
        Assert.False(new GeminiCostGovernor(options: null, recordCost: (_, _) => { }).HasRequestCeiling); // no Budget section at all
    }

    [Fact]
    public void CheckEstimatedRequestCost_InputOnly_ThrowsExactlyAboveTheCeiling_NotAtOrBelow()
    {
        // 1 token = $0.01, for clean cent-level boundary math (same trick as the daily-budget test).
        var pricing = MakePricing(inputPerMillion: 10_000m, outputPerMillion: 0m, cachedPerMillion: 0m);
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: 5.00m);

        // Exactly at the ceiling ($5.00): must NOT throw (only strictly-above rejects, matching
        // CheckBudget's ">=" being the daily-total's own boundary — here the estimate itself must
        // exceed, not merely equal, the ceiling).
        var atCeiling = Record.Exception(() => governor.CheckEstimatedRequestCost(TestModel, inputTokens: 500, maxOutputTokens: null));
        Assert.Null(atCeiling);

        var thrown = Assert.Throws<GeminiRequestCostExceededException>(
            () => governor.CheckEstimatedRequestCost(TestModel, inputTokens: 501, maxOutputTokens: null));
        Assert.True(thrown.EstimatedCostUsd > 5.00m);
        Assert.Equal(5.00m, thrown.MaxCostPerRequestUsd);
    }

    [Fact]
    public void CheckEstimatedRequestCost_WithoutMaxOutputTokens_OnlyBoundsInput()
    {
        var pricing = MakePricing(inputPerMillion: 1.00m, outputPerMillion: 1_000_000m, cachedPerMillion: 0m); // output rate absurdly high on purpose
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: 10.00m);

        // Input alone (1M tokens @ $1.00/M = $1.00) is well under the $10 ceiling. If the output rate
        // were somehow applied despite maxOutputTokens being null, this would throw (even 1 output
        // token would cost $1,000,000). It must not.
        var estimate = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 1_000_000, maxOutputTokens: null);

        Assert.Equal(1.00m, estimate); // exactly the input cost — output side contributed nothing
    }

    [Fact]
    public void CheckEstimatedRequestCost_WithMaxOutputTokens_BoundsBothSides()
    {
        var pricing = MakePricing(inputPerMillion: 1.00m, outputPerMillion: 2.00m, cachedPerMillion: 0m);
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: 10.00m);

        var estimate = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 1_000_000, maxOutputTokens: 1_000_000);

        // inputCost 1M/1M*1.00 = 1.00; outputCost 1M/1M*2.00 = 2.00
        Assert.Equal(3.00m, estimate);
    }

    [Fact]
    public void CheckEstimatedRequestCost_HighVolumeTier_SelectedFromInputTokenCount()
    {
        var pricing = MakePricing(
            inputPerMillion: 1.00m, outputPerMillion: 0m, cachedPerMillion: 0m,
            highVolumeTier: new HighVolumeTier
            {
                ThresholdTokens = 100,
                InputPerMillionTokensUsd = 10.00m,
                OutputPerMillionTokensUsd = 0m,
                CachedInputPerMillionTokensUsd = 0m,
            });
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: 1_000.00m);

        var atThreshold = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 100, maxOutputTokens: null);
        var aboveThreshold = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 101, maxOutputTokens: null);

        Assert.Equal(100m / 1_000_000m * 1.00m, atThreshold);
        Assert.Equal(101m / 1_000_000m * 10.00m, aboveThreshold);
    }

    [Fact]
    public void CheckEstimatedRequestCost_NoCeilingConfigured_NeverThrows_ReturnsZero()
    {
        var pricing = MakePricing(inputPerMillion: 1_000_000m, outputPerMillion: 0m, cachedPerMillion: 0m);
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: null); // ceiling not set

        var estimate = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 1_000_000_000, maxOutputTokens: null);

        Assert.Equal(0m, estimate); // not even computed — nothing to compare against
    }

    [Fact]
    public void CheckEstimatedRequestCost_Disabled_NeverThrows_EvenWithCeilingConfigured()
    {
        var pricing = MakePricing(inputPerMillion: 1_000_000m, outputPerMillion: 0m, cachedPerMillion: 0m);
        var governor = CreateGovernor(pricing, maxCostPerRequestUsd: 0.01m, enabled: false);

        var ex = Record.Exception(() =>
            governor.CheckEstimatedRequestCost(TestModel, inputTokens: 1_000_000_000, maxOutputTokens: null));

        Assert.Null(ex);
    }

    [Fact]
    public void CheckEstimatedRequestCost_UnknownModel_NeverThrows_ReturnsZero_WarnsOnce()
    {
        var logger = new CountingLogger();
        var governor = new GeminiCostGovernor(
            new BudgetOptions { MaxCostPerRequestUsd = 0.01m }, (_, _) => { }, logger);

        var estimate = governor.CheckEstimatedRequestCost("some-unpriced-model", inputTokens: 1_000_000_000, maxOutputTokens: null);

        Assert.Equal(0m, estimate);
        Assert.Equal(1, logger.WarningCount);
    }

    [Fact]
    public void CheckEstimatedRequestCost_NullBudget_IsZeroOverheadNoOp()
    {
        var governor = new GeminiCostGovernor(options: null, recordCost: (_, _) => { });

        Assert.False(governor.HasRequestCeiling);
        var estimate = governor.CheckEstimatedRequestCost(TestModel, inputTokens: 1_000_000_000, maxOutputTokens: null);
        Assert.Equal(0m, estimate);
    }

    private sealed class CountingLogger : ILogger<GeminiCostGovernor>
    {
        public int WarningCount { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningCount++;
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
