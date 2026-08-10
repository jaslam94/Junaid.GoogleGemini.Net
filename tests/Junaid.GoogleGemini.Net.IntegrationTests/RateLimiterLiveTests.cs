using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live test for <see cref="GeminiRateLimiter"/> -- previously only unit-tested against a mocked HTTP
/// handler, never confirmed to actually reject real, concurrent calls against the live API. Uses its
/// own DI container (not <see cref="GeminiFixture"/>) so it can configure a deliberately restrictive
/// <see cref="RateLimitOptions"/>.
/// </summary>
[Collection("Live")]
public class RateLimiterLiveTests
{
    [RequiresGeminiKey]
    public async Task ConcurrentCalls_ExceedingBurstAndQueue_RejectsExcessWithRateLimitException()
    {
        var key = Environment.GetEnvironmentVariable("GeminiApiKey")!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(options =>
        {
            options.ApiKey = key;
            options.DefaultModel = "gemini-3.5-flash-lite";
            // RequestsPerMinute=60 -> 1 token/sec, TokenLimit=2 (burst), QueueLimit=10 (fixed in
            // GeminiRateLimiter) -> at most 2 + 10 = 12 concurrent acquisitions succeed immediately or
            // queue; any beyond that are rejected synchronously, no network call involved.
            options.RateLimit = new RateLimitOptions { Enabled = true, RequestsPerMinute = 60 };
        });
        var gemini = services.BuildServiceProvider().GetRequiredService<IGeminiService>();

        var requestOptions = new GeminiRequestOptions { MaxTokens = 1, ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal };
        // Cancel promptly: the point is proving some calls are rejected the instant the burst+queue
        // are full, not waiting ~10s for the queued ones to actually drain and complete real calls.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            try
            {
                await gemini.GenerateAsync("Reply with: hi", requestOptions, cts.Token);
                return "ok";
            }
            catch (GeminiRateLimitException)
            {
                return "rate-limited";
            }
            catch (OperationCanceledException)
            {
                return "cancelled";
            }
        }));

        var counts = outcomes.GroupBy(o => o).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(counts.TryGetValue("rate-limited", out var rateLimitedCount) && rateLimitedCount > 0,
            $"Expected at least one rate-limited rejection among 50 concurrent calls. Outcomes: {string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }
}
