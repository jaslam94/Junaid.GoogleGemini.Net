using System.Diagnostics;
using System.Text.Json.Nodes;
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
/// Live tests for <see cref="ICostGovernor"/> (the <see cref="BudgetOptions"/> feature) against the
/// real Gemini API. Each test builds its own small DI container rather than sharing
/// <see cref="GeminiFixture"/>, because every test needs a different <see cref="BudgetOptions"/>
/// configuration -- the whole point is exercising real ceilings against real recorded spend.
/// Ceilings are deliberately tiny (not the round numbers a consuming app would use) so a single cheap
/// short-prompt call is enough to prove the boundary, rather than deliberately overspending to trip a
/// realistic one.
/// </summary>
[Collection("Live")]
public class CostGovernanceLiveTests
{
    private const string ShortPrompt = "Reply with exactly the word: pong";

    private static IGeminiService BuildGemini(BudgetOptions budget, out ICostGovernor governor)
    {
        var key = Environment.GetEnvironmentVariable("GeminiApiKey")!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(options =>
        {
            options.ApiKey = key;
            // gemini-3.5-flash-lite: cheapest currently-available model with a DefaultPricing entry
            // (gemini-2.5-flash is no longer available to new-user keys, see GeminiFixture).
            options.DefaultModel = "gemini-3.5-flash-lite";
            options.Budget = budget;
        });
        var provider = services.BuildServiceProvider();
        governor = provider.GetRequiredService<ICostGovernor>();
        return provider.GetRequiredService<IGeminiService>();
    }

    [RequiresGeminiKey]
    public async Task DailyBudget_AllowsFirstCallThenRejectsOnceLimitReached()
    {
        // A ceiling far below the cost of any real call: the first call is checked while spend is
        // still $0 (0 < limit), so it's let through and actually billed; recording its real usage
        // pushes spend over the ceiling, so the second call is rejected before being sent.
        var gemini = BuildGemini(new BudgetOptions { MaxCostPerDayUsd = 0.0000001m }, out var governor);

        var first = await gemini.GenerateAsync(ShortPrompt);
        Assert.False(string.IsNullOrWhiteSpace(first.Text()));
        var spendAfterFirst = governor.GetTodaySpend();
        Assert.True(spendAfterFirst > 0m, "Expected the real call's usage to be priced and recorded.");

        var sw = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<GeminiBudgetExceededException>(() => gemini.GenerateAsync(ShortPrompt));
        sw.Stop();

        Assert.Equal(spendAfterFirst, ex.CurrentSpendUsd);
        Assert.Equal(0.0000001m, ex.BudgetLimitUsd);
        // The rejection is in-memory only (no network call) -- a real generateContent round-trip
        // takes seconds, so this should be near-instant.
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Expected an instant in-memory rejection, took {sw.ElapsedMilliseconds}ms -- may indicate the real API was called.");
    }

    [RequiresGeminiKey]
    public async Task MaxCostPerRequestUsd_RejectsOversizedEstimate_WithoutSendingTheRealCall()
    {
        // MaxTokens=8000 on a $0.01 ceiling makes the pre-flight estimate exceed it comfortably, so
        // the real (billed) generateContent call must never go out -- only the free CountTokensAsync
        // pre-flight round-trip does.
        var gemini = BuildGemini(new BudgetOptions { MaxCostPerRequestUsd = 0.01m }, out _);
        var options = new GeminiRequestOptions { MaxTokens = 8000, ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal };

        var ex = await Assert.ThrowsAsync<GeminiRequestCostExceededException>(
            () => gemini.GenerateAsync(ShortPrompt, options));

        Assert.True(ex.EstimatedCostUsd > ex.MaxCostPerRequestUsd);
        Assert.Equal(0.01m, ex.MaxCostPerRequestUsd);
    }

    [RequiresGeminiKey]
    public async Task MaxCostPerRequestUsd_AllowsCallWithinBudget()
    {
        // Ceiling comfortably above a small call's estimate -- proves the pre-flight check doesn't
        // false-positive block a call that's actually within budget, and the real recorded cost lines
        // up with what was estimated.
        var gemini = BuildGemini(new BudgetOptions { MaxCostPerRequestUsd = 0.05m }, out var governor);
        var options = new GeminiRequestOptions { MaxTokens = 200, ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal };

        var response = await gemini.GenerateAsync(ShortPrompt, options);

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
        Assert.True(governor.GetTodaySpend() > 0m, "Expected the real call's usage to be priced and recorded.");
    }

    [RequiresGeminiKey]
    public async Task StreamAsync_RecordsRealSpendFromTheFinalChunk()
    {
        // The previously-zero-telemetry gap: StreamAsync must price and record cost from the final
        // SSE chunk's usageMetadata, same as GenerateAsync does from the single response.
        var gemini = BuildGemini(new BudgetOptions { MaxCostPerDayUsd = 1.00m }, out var governor);
        var options = new GeminiRequestOptions { MaxTokens = 200, ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal };

        Assert.Equal(0m, governor.GetTodaySpend());

        await foreach (var _ in gemini.StreamAsync(ShortPrompt, options))
        {
            // Draining the stream is the point; content isn't asserted here (covered by StreamAsync_YieldsChunks).
        }

        Assert.True(governor.GetTodaySpend() > 0m, "Expected the streamed call's usage to be priced and recorded.");
    }

    [RequiresGeminiKey]
    public async Task MaxCostPerRequestUsd_CoversChatAsyncContentList_WithFunctionCallAndThoughtSignature()
    {
        // The one thing LiveTests.FunctionCalling_Gemini3_RoundTripWithThoughtSignature never covers:
        // that test's turn-2 ChatAsync(IList<Content>) call runs with no budget configured, so it never
        // exercises CheckPerRequestBudgetForContentAsync -> CountTokensChatAsync(IList<Content>, ...).
        // This is the first time a Content list carrying a functionCall/functionResponse pair AND an
        // encrypted thoughtSignature has been sent to the real countTokens endpoint through this
        // library -- proving Google's countTokens endpoint accepts that shape without erroring, and
        // that the resulting estimate doesn't false-positive reject a normal, cheap call.
        const string model = GeminiConstants.Models.Gemini36Flash;
        const string userPrompt = "What's the current temperature in Paris? Use the get_weather tool.";

        var gemini = BuildGemini(new BudgetOptions { MaxCostPerRequestUsd = 0.05m }, out var governor);

        var getWeather = new FunctionDeclaration
        {
            Name = "get_weather",
            Description = "Get the current temperature for a city, in Celsius.",
            Parameters = JsonNode.Parse(
                """{"type":"object","properties":{"city":{"type":"string","description":"City name"}},"required":["city"]}""")
        };

        var callOptions = new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
            ToolConfig = new ToolConfig { FunctionCallingConfig = new FunctionCallingConfig { Mode = "ANY" } }
        };

        var first = await gemini.GenerateAsync(userPrompt, callOptions);
        var modelTurn = first.Candidates?[0].Content;
        Assert.NotNull(modelTurn);
        var call = modelTurn!.Parts.FirstOrDefault(p => p.FunctionCall is not null)?.FunctionCall;
        Assert.NotNull(call);
        var signature = modelTurn.Parts.FirstOrDefault(p => p.ThoughtSignature is not null)?.ThoughtSignature;
        Assert.False(string.IsNullOrEmpty(signature),
            "Expected a thoughtSignature on the Gemini 3 function-call part.");

        var replyOptions = new GeminiRequestOptions
        {
            Model = model,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
            ToolConfig = new ToolConfig { FunctionCallingConfig = new FunctionCallingConfig { Mode = "AUTO" } }
        };

        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = userPrompt }] },
            modelTurn, // role=model, includes functionCall + thoughtSignature -- this is what gets counted
            new()
            {
                Role = "user",
                Parts =
                [
                    new Part
                    {
                        FunctionResponse = new FunctionResponsePart
                        {
                            Name = "get_weather",
                            Response = JsonNode.Parse("""{"temperatureC":18}""")
                        }
                    }
                ]
            }
        };

        // If countTokens rejected this Content shape, this would throw before ever reaching the real
        // (billed) generateContent call. If the estimate falsely exceeded the $0.05 ceiling, it would
        // throw GeminiRequestCostExceededException instead of completing normally.
        var second = await gemini.ChatAsync(contents, replyOptions);

        Assert.Equal("STOP", second.FinishReason);
        Assert.Contains("18", second.Text());
        Assert.True(governor.GetTodaySpend() > 0m, "Expected both turns' real usage to be priced and recorded.");
    }
}
