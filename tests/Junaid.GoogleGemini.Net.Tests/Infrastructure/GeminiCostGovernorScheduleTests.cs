using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
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
}
