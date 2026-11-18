using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Pins the Gemini 4 behaviors that this library documents from live observation on 2026-11-16 (see
/// <c>PLAN-gemini-4.md</c>, section 4). These tests replace the first four probes in
/// <c>Gemini4ProbeLiveTests</c>. Google changes models without notice. If one of these fails, the docs
/// and the release notes are now wrong, and the failing test says which claim.
///
/// Hydrogen has a free tier, so its tests use <see cref="RequiresGeminiKeyAttribute"/>. Carbon and Argon
/// are paid only, so every test that calls them uses <see cref="RequiresPaidGeminiKeyAttribute"/>.
/// Every test costs a fraction of a cent.
/// </summary>
[Collection("Live")]
public class Gemini4BehaviorLiveTests(GeminiFixture fixture)
{
    private const string Prompt = "Reply with the single word OK.";

    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    // ----- each model answers -----

    [RequiresGeminiKey]
    public async Task Hydrogen_Answers() => await AssertAnswers(GeminiConstants.Models.Gemini4Hydrogen);

    [RequiresPaidGeminiKey]
    public async Task Carbon_Answers() => await AssertAnswers(GeminiConstants.Models.Gemini4Carbon);

    [RequiresPaidGeminiKey]
    public async Task Argon_Answers() => await AssertAnswers(GeminiConstants.Models.Gemini4Argon);

    private async Task AssertAnswers(string model)
    {
        var response = await Gemini.GenerateAsync(Prompt, new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
        });

        Assert.Contains("OK", response.Text(), StringComparison.OrdinalIgnoreCase);
        // Google reports the model that answered. A different value would mean a routing change that
        // makes the governor's prices wrong.
        Assert.Equal(model, response.ModelVersion);
    }

    // ----- thinking levels -----

    [RequiresGeminiKey]
    public async Task ThinkingLevelXHigh_OnHydrogen_Succeeds()
    {
        var response = await Gemini.GenerateAsync(Prompt, new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini4Hydrogen,
            ThinkingLevel = GeminiConstants.ThinkingLevels.XHigh,
        });

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
    }

    [RequiresPaidGeminiKey]
    public async Task ThinkingLevelXHigh_OnCarbon_IsRejectedWithBadRequest()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini4Carbon,
            ThinkingLevel = GeminiConstants.ThinkingLevels.XHigh,
        };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Gemini.GenerateAsync(Prompt, options));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("XHIGH", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task ThinkingLevelMinimal_OnHydrogen_IsRejectedWithBadRequest()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini4Hydrogen,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal,
        };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Gemini.GenerateAsync(Prompt, options));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("MINIMAL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ----- explicit caching -----

    [RequiresGeminiKey]
    public async Task ExplicitCaching_OnHydrogen_IsRejectedWithBadRequest()
    {
        var caching = fixture.Get<ICachingService>();
        var content = new CachedContent
        {
            Model = $"models/{GeminiConstants.Models.Gemini4Hydrogen}",
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "hello" }] }],
            Ttl = "60s",
        };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => caching.CreateAsync(content));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("Explicit caching is not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ----- store -----

    [RequiresGeminiKey]
    public async Task StoreFalse_OnHydrogen_IsAccepted_AndReturnsText()
    {
        var response = await Gemini.GenerateAsync(Prompt, new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini4Hydrogen,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Store = false,
        });

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
    }

    // ----- the cost governor prices a real call the way Google reports it -----

    [RequiresGeminiKey]
    public async Task CostGovernor_PricesARealHydrogenCall_FromGooglesUsage() =>
        await AssertGovernorMatchesUsage(GeminiConstants.Models.Gemini4Hydrogen);

    [RequiresPaidGeminiKey]
    public async Task CostGovernor_PricesARealCarbonCall_FromGooglesUsage() =>
        await AssertGovernorMatchesUsage(GeminiConstants.Models.Gemini4Carbon);

    private static async Task AssertGovernorMatchesUsage(string model)
    {
        // A budget that no single call can reach turns the governor on without ever blocking the call.
        var gemini = BuildGemini(new BudgetOptions { MaxCostPerDayUsd = 5m }, out var governor);
        Assert.Equal(0m, governor.GetTodaySpend());

        var before = DateTime.UtcNow;
        var response = await gemini.GenerateAsync(Prompt, new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
        });
        var after = DateTime.UtcNow;

        var usage = response.Usage;
        Assert.NotNull(usage);
        // A guard against a vacuous pass. A zero usage would make any price match.
        Assert.True(usage!.PromptTokenCount > 0, "Google reported no prompt tokens.");
        Assert.True(usage.CandidatesTokenCount + usage.ThoughtsTokenCount > 0, "Google reported no output tokens.");

        var recorded = governor.GetTodaySpend();

        // The governor reads its clock when the call returns. A rate change can fall between the two
        // clock readings only in the second it happens, so accept the cost at either reading.
        var expected = new[] { ExpectedCostUsd(model, usage, before), ExpectedCostUsd(model, usage, after) };
        Assert.True(
            expected.Contains(recorded),
            $"{model}: the governor recorded {recorded} USD, but Google's usage " +
            $"(prompt={usage.PromptTokenCount}, cached={usage.CachedContentTokenCount}, " +
            $"candidates={usage.CandidatesTokenCount}, thoughts={usage.ThoughtsTokenCount}, " +
            $"toolUse={usage.ToolUsePromptTokenCount}) prices at {string.Join(" or ", expected)} USD.");
    }

    /// <summary>
    /// The price in USD of one call, worked out here from Google's pricing page (read 2026-11-18) and
    /// not from the library's table. Thought tokens count as output. Cached tokens use the cached rate.
    /// Carbon and Argon double at 00:00 UTC on 2026-12-01.
    /// </summary>
    private static decimal ExpectedCostUsd(string model, UsageMetadata usage, DateTime utcNow)
    {
        var afterRise = utcNow >= new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var (input, output, cached) = model switch
        {
            GeminiConstants.Models.Gemini4Hydrogen => (1.50m, 7.50m, 0.075m),
            GeminiConstants.Models.Gemini4Carbon or GeminiConstants.Models.Gemini4Argon =>
                afterRise ? (4.00m, 20.00m, 0.20m) : (2.00m, 10.00m, 0.10m),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "No expected price for this model."),
        };

        var billableInput = Math.Max(0, usage.PromptTokenCount - usage.CachedContentTokenCount)
            + Math.Max(0, usage.ToolUsePromptTokenCount);
        return billableInput / 1_000_000m * input
            + usage.CachedContentTokenCount / 1_000_000m * cached
            + (usage.CandidatesTokenCount + usage.ThoughtsTokenCount) / 1_000_000m * output;
    }

    private static IGeminiService BuildGemini(BudgetOptions budget, out ICostGovernor governor)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(options =>
        {
            options.ApiKey = Environment.GetEnvironmentVariable("GeminiApiKey")!;
            options.DefaultModel = GeminiConstants.Models.Gemini4Hydrogen;
            options.Budget = budget;
        });
        var provider = services.BuildServiceProvider();
        governor = provider.GetRequiredService<ICostGovernor>();
        return provider.GetRequiredService<IGeminiService>();
    }
}
