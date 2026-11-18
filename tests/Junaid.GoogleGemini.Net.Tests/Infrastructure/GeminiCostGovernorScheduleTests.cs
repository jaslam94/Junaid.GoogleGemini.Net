using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// Scheduled pricing. Google's introductory prices for the 3.6 and 3.8 Flash models and the 3.8 TTS models
/// end on 2026-12-31. The standard rates start on 2027-01-01. Rates read from Google's pricing page on
/// 2026-10-10.
/// </summary>
public class GeminiCostGovernorScheduleTests
{
    private static readonly DateTime LastIntroSecond = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime FirstStandardSecond = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static decimal CostOfOneMillionInOneMillionOut(string model, DateTime now)
    {
        var governor = new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now);
        return governor.RecordSpend(model, new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000 });
    }

    [Theory]
    [InlineData("gemini-3.8-flash", 4.50, 9.00)]            // 0.75 + 3.75, then 1.50 + 7.50
    [InlineData("gemini-3.6-flash", 4.50, 9.00)]
    [InlineData("gemini-3.8-flash-tts", 9.50, 19.00)]       // 0.50 + 9.00, then 1.00 + 18.00
    [InlineData("gemini-3.8-flash-lite-tts", 6.50, 13.00)]  // 0.50 + 6.00, then 1.00 + 12.00
    public void BuiltInPricing_SwitchesToTheStandardRate_AtTheStartOf2027(string model, double intro, double standard)
    {
        Assert.Equal((decimal)intro, CostOfOneMillionInOneMillionOut(model, LastIntroSecond));
        Assert.Equal((decimal)standard, CostOfOneMillionInOneMillionOut(model, FirstStandardSecond));
        Assert.Equal((decimal)standard, CostOfOneMillionInOneMillionOut(model, new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("gemini-3.5-flash", "gemini-3.6-flash")]
    [InlineData("gemini-3.7-flash", "gemini-3.8-flash")]
    public void RedirectedModels_AreBilledAtTheRateOfTheModelThatAnswers(string requested, string answers)
    {
        var now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(CostOfOneMillionInOneMillionOut(answers, now), CostOfOneMillionInOneMillionOut(requested, now));
        Assert.Equal(CostOfOneMillionInOneMillionOut(answers, FirstStandardSecond), CostOfOneMillionInOneMillionOut(requested, FirstStandardSecond));
    }

    [Fact]
    public void NanoBanana21_IsPriced_AtTheImageOutputRate_OnBothSidesOfTheDate()
    {
        // $1.50 in, $30.00 out. This model has no introductory price.
        Assert.Equal(31.50m, CostOfOneMillionInOneMillionOut("gemini-nano-banana-2.1", LastIntroSecond));
        Assert.Equal(31.50m, CostOfOneMillionInOneMillionOut("gemini-nano-banana-2.1", FirstStandardSecond));
    }

    [Fact]
    public void ModelsWithoutASchedule_AreUnaffectedByTheClock()
    {
        Assert.Equal(
            CostOfOneMillionInOneMillionOut("gemini-3.5-flash-lite", LastIntroSecond),
            CostOfOneMillionInOneMillionOut("gemini-3.5-flash-lite", FirstStandardSecond));
        Assert.Equal(2.80m, CostOfOneMillionInOneMillionOut("gemini-3.5-flash-lite", FirstStandardSecond)); // 0.30 + 2.50
    }

    [Fact]
    public void ADateRunsThroughTheSameGovernorInstance_WithoutARestart()
    {
        var now = LastIntroSecond;
        var governor = new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now);
        var usage = new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000 };

        var before = governor.RecordSpend("gemini-3.8-flash", usage);
        now = FirstStandardSecond;
        var after = governor.RecordSpend("gemini-3.8-flash", usage);

        Assert.Equal(4.50m, before);
        Assert.Equal(9.00m, after);
    }

    [Fact]
    public void PerRequestEstimate_AlsoFollowsTheSchedule()
    {
        // 1M input tokens, no output bound. Intro: $0.75. Standard: $1.50.
        decimal Estimate(DateTime now) => new GeminiCostGovernor(
                new BudgetOptions { Enabled = true, MaxCostPerRequestUsd = 100m }, (_, _) => { }, utcNowProvider: () => now)
            .CheckEstimatedRequestCost("gemini-3.8-flash", 1_000_000, null);

        Assert.Equal(0.75m, Estimate(LastIntroSecond));
        Assert.Equal(1.50m, Estimate(FirstStandardSecond));
    }

    [Fact]
    public void AnOverrideCanCarryItsOwnChainedSchedule()
    {
        static ModelPricing Rate(decimal r) => new()
        {
            InputPerMillionTokensUsd = r,
            OutputPerMillionTokensUsd = r,
            CachedInputPerMillionTokensUsd = r,
        };

        var third = Rate(4m);
        var second = Rate(2m);
        second.NextPricing = third;
        second.NextPricingEffectiveFromUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var first = Rate(1m);
        first.NextPricing = second;
        first.NextPricingEffectiveFromUtc = new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        decimal CostAt(DateTime now) => new GeminiCostGovernor(
                new BudgetOptions { Enabled = true, ModelPricingOverrides = new Dictionary<string, ModelPricing> { ["m"] = first } },
                (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("m", new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000 });

        Assert.Equal(2m, CostAt(new DateTime(2028, 6, 1, 0, 0, 0, DateTimeKind.Utc)));  // 1 + 1
        Assert.Equal(4m, CostAt(new DateTime(2029, 6, 1, 0, 0, 0, DateTimeKind.Utc)));  // 2 + 2
        Assert.Equal(8m, CostAt(new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc)));  // 4 + 4
    }

    [Theory]
    [InlineData("2027-01-01T00:00:00Z")]       // what a user would write for a UTC instant
    [InlineData("2027-01-01T05:00:00+05:00")]  // the same instant with an offset
    public void AScheduleBindsFromConfiguration_LikeAnAppsettingsFile(string effective)
    {
        // The binder turns a date string into a DateTime of Kind Local. The governor must convert it to UTC.
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Enabled"] = "true",
                ["ModelPricingOverrides:m:InputPerMillionTokensUsd"] = "1",
                ["ModelPricingOverrides:m:OutputPerMillionTokensUsd"] = "1",
                ["ModelPricingOverrides:m:CachedInputPerMillionTokensUsd"] = "1",
                ["ModelPricingOverrides:m:NextPricingEffectiveFromUtc"] = effective,
                ["ModelPricingOverrides:m:NextPricing:InputPerMillionTokensUsd"] = "2",
                ["ModelPricingOverrides:m:NextPricing:OutputPerMillionTokensUsd"] = "2",
                ["ModelPricingOverrides:m:NextPricing:CachedInputPerMillionTokensUsd"] = "2",
            })
            .Build();
        var budget = new BudgetOptions();
        Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(config, budget);

        decimal CostAt(DateTime now) => new GeminiCostGovernor(budget, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("m", new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000 });

        Assert.Equal(2m, CostAt(LastIntroSecond));        // 1 + 1, still before the date
        Assert.Equal(4m, CostAt(FirstStandardSecond));    // 2 + 2, on the date
    }

    [Fact]
    public void AScheduleWithADateButNoNextPricing_IsIgnored()
    {
        var pricing = new ModelPricing
        {
            InputPerMillionTokensUsd = 1m,
            OutputPerMillionTokensUsd = 1m,
            CachedInputPerMillionTokensUsd = 1m,
            NextPricingEffectiveFromUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var governor = new GeminiCostGovernor(
            new BudgetOptions { Enabled = true, ModelPricingOverrides = new Dictionary<string, ModelPricing> { ["m"] = pricing } },
            (_, _) => { }, utcNowProvider: () => FirstStandardSecond);

        Assert.Equal(2m, governor.RecordSpend("m", new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000 }));
    }

    // Google deprecated gemini-2.5-pro and gemini-2.5-flash on 2026-11-02. From 2026-12-01 it routes
    // them to gemini-3.8-flash and gemini-3.5-flash-lite and bills at those models' rates. The 2.5 Pro
    // route uses 08:00 UTC, the end of 2026-12-01 in US Pacific time, because the change lowers the price
    // and an early switch would undercount a budget. Rates are from Google's pricing page, 2026-11-15.
    private static readonly DateTime LastSecondBeforeProRouting = new(2026, 12, 1, 7, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime FirstSecondOfProRouting = new(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Gemini25Pro_IsBilledAtItsOwnRate_UntilTheRoutingStarts_ThenAtTheRoutedModelsRate()
    {
        // 100,000 tokens each way. This is under the 200,000-token threshold of the high-volume tier, so
        // the base rates apply. (One million prompt tokens would be above the threshold.)
        decimal Cost(DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("gemini-2.5-pro", new UsageMetadata { PromptTokenCount = 100_000, CandidatesTokenCount = 100_000 });

        Assert.Equal(1.125m, Cost(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc))); // 0.1 x 1.25 + 0.1 x 10.00
        Assert.Equal(1.125m, Cost(LastSecondBeforeProRouting));
        Assert.Equal(0.450m, Cost(FirstSecondOfProRouting));  // 3.8 Flash intro: 0.1 x 0.75 + 0.1 x 3.75
        Assert.Equal(0.450m, Cost(LastIntroSecond));
        Assert.Equal(0.900m, Cost(FirstStandardSecond));      // 3.8 Flash standard: 0.1 x 1.50 + 0.1 x 7.50
    }

    [Fact]
    public void Gemini25Pro_FollowsTheRoutedModelsOwnChangeOnNewYearsDay()
    {
        // The routed model, gemini-3.8-flash, doubles on 2027-01-01. The chained schedule must reach it.
        Assert.Equal(9.00m, CostOfOneMillionInOneMillionOut("gemini-2.5-pro", FirstStandardSecond));    // 1.50 + 7.50
        Assert.Equal(
            CostOfOneMillionInOneMillionOut("gemini-3.8-flash", FirstStandardSecond),
            CostOfOneMillionInOneMillionOut("gemini-2.5-pro", FirstStandardSecond));
        Assert.Equal(
            CostOfOneMillionInOneMillionOut("gemini-3.8-flash", FirstSecondOfProRouting),
            CostOfOneMillionInOneMillionOut("gemini-2.5-pro", FirstSecondOfProRouting));
    }

    [Fact]
    public void Gemini25Pro_LosesItsHighVolumeTier_WhenTheRoutingStarts()
    {
        // 300,000 prompt tokens is above the 200,000 threshold. 3.8 Flash has no high-volume tier.
        decimal Cost(DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("gemini-2.5-pro", new UsageMetadata { PromptTokenCount = 300_000, CandidatesTokenCount = 100_000 });

        Assert.Equal(2.25m, Cost(LastSecondBeforeProRouting));  // 0.3 x 2.50 + 0.1 x 15.00
        Assert.Equal(0.60m, Cost(FirstSecondOfProRouting));     // 0.3 x 0.75 + 0.1 x 3.75
    }

    // Gemini 4 Carbon and Argon double in price on 2026-12-01. Google gives no time zone, and the change
    // raises the price, so the governor switches at the earliest plausible instant, 00:00 UTC. Rates are
    // from Google's pricing page, 2026-11-18.
    private static readonly DateTime LastSecondBeforeGemini4Rise = new(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime FirstSecondOfGemini4Rise = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("gemini-4-carbon")]
    [InlineData("gemini-4-argon")]
    public void Gemini4FrontierModels_DoubleInPrice_AtMidnightUtcOnTheFirstOfDecember(string model)
    {
        // 1M in and 1M out. Until 2026-11-30: $2.00 + $10.00. From 2026-12-01: $4.00 + $20.00.
        Assert.Equal(12.00m, CostOfOneMillionInOneMillionOut(model, new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(12.00m, CostOfOneMillionInOneMillionOut(model, LastSecondBeforeGemini4Rise));
        Assert.Equal(24.00m, CostOfOneMillionInOneMillionOut(model, FirstSecondOfGemini4Rise));
        Assert.Equal(24.00m, CostOfOneMillionInOneMillionOut(model, new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("gemini-4-carbon")]
    [InlineData("gemini-4-argon")]
    public void Gemini4FrontierModels_PriceCachedTokensAtTheCachedRate_OnBothSidesOfTheRise(string model)
    {
        // 1M prompt tokens, of which 500,000 are cached, and 1M output tokens.
        // Before: 0.5 x 2.00 + 0.5 x 0.10 + 1 x 10.00 = 11.05. After: 0.5 x 4.00 + 0.5 x 0.20 + 1 x 20.00 = 22.10.
        decimal Cost(DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend(model, new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000, CachedContentTokenCount = 500_000 });

        Assert.Equal(11.05m, Cost(LastSecondBeforeGemini4Rise));
        Assert.Equal(22.10m, Cost(FirstSecondOfGemini4Rise));
    }

    [Fact]
    public void Gemini4Models_CountThoughtTokensAsOutput_OnBothSidesOfTheRise()
    {
        // 100,000 prompt, 100,000 answer and 300,000 thought tokens. Output is 400,000 tokens.
        // Before: 0.1 x 2.00 + 0.4 x 10.00 = 4.20. After: 0.1 x 4.00 + 0.4 x 20.00 = 8.40.
        decimal Cost(DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("gemini-4-carbon", new UsageMetadata { PromptTokenCount = 100_000, CandidatesTokenCount = 100_000, ThoughtsTokenCount = 300_000 });

        Assert.Equal(4.20m, Cost(LastSecondBeforeGemini4Rise));
        Assert.Equal(8.40m, Cost(FirstSecondOfGemini4Rise));
    }

    [Theory]
    [InlineData("2026-11-18T00:00:00Z")]
    [InlineData("2026-11-30T23:59:59Z")]
    [InlineData("2026-12-01T00:00:00Z")]
    [InlineData("2027-06-01T00:00:00Z")]
    public void Gemini4Hydrogen_HasOneRate_OnEveryDate(string instant)
    {
        // $1.50 in and $7.50 out, with no schedule.
        var now = DateTime.Parse(instant, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

        Assert.Equal(9.00m, CostOfOneMillionInOneMillionOut("gemini-4-hydrogen", now));
    }

    [Fact]
    public void Gemini4Hydrogen_PricesCachedTokensAtTheCachedRate()
    {
        // 0.5 x 1.50 + 0.5 x 0.075 + 1 x 7.50 = 8.2875, before and after the Carbon and Argon rise.
        decimal Cost(DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend("gemini-4-hydrogen", new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000, CachedContentTokenCount = 500_000 });

        Assert.Equal(8.2875m, Cost(LastSecondBeforeGemini4Rise));
        Assert.Equal(8.2875m, Cost(FirstSecondOfGemini4Rise));
    }

    [Fact]
    public void Gemini4Models_HaveNoHighVolumeTier()
    {
        // Google prices the full 1M context at one rate. 900,000 prompt tokens must not trigger a tier.
        decimal Cost(string model, DateTime now) => new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now)
            .RecordSpend(model, new UsageMetadata { PromptTokenCount = 900_000, CandidatesTokenCount = 0 });

        Assert.Equal(1.35m, Cost("gemini-4-hydrogen", FirstSecondOfGemini4Rise));    // 0.9 x 1.50
        Assert.Equal(1.80m, Cost("gemini-4-carbon", LastSecondBeforeGemini4Rise));   // 0.9 x 2.00
        Assert.Equal(3.60m, Cost("gemini-4-argon", FirstSecondOfGemini4Rise));       // 0.9 x 4.00
    }

    [Fact]
    public void Gemini4Models_AreKnownToTheGovernor()
    {
        foreach (var model in new[] { "gemini-4-hydrogen", "gemini-4-carbon", "gemini-4-argon" })
            Assert.True(GeminiCostGovernor.DefaultPricing.ContainsKey(model), model);
    }

    [Theory]
    [InlineData("2026-11-15T00:00:00Z")]
    [InlineData("2026-12-01T07:59:59Z")]
    [InlineData("2026-12-01T08:00:00Z")]
    [InlineData("2027-06-01T00:00:00Z")]
    public void Gemini25Flash_HasTheSameRatesAsTheModelItRoutesTo_OnEveryDate(string instant)
    {
        // gemini-2.5-flash routes to gemini-3.5-flash-lite on 2026-12-01. Both cost $0.30 in, $2.50 out and
        // $0.03 cached today, so the built-in table needs no schedule for it. If Google changes one of
        // them, this test fails and the table needs a schedule.
        var now = DateTime.Parse(instant, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        var governor = new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { }, utcNowProvider: () => now);
        var usage = new UsageMetadata { PromptTokenCount = 1_000_000, CandidatesTokenCount = 1_000_000, CachedContentTokenCount = 500_000 };

        Assert.Equal(governor.RecordSpend("gemini-3.5-flash-lite", usage), governor.RecordSpend("gemini-2.5-flash", usage));
    }
}
