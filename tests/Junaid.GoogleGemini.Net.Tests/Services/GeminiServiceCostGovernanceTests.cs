using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

/// <summary>
/// Covers how <see cref="GeminiService"/> wires <see cref="BudgetOptions.MaxCostPerRequestUsd"/>
/// pre-flight estimation into its generation call sites: the gate that skips the extra
/// <c>CountTokensAsync</c> round-trip entirely when no ceiling is configured, the rejection path
/// before the real request is ever sent, the MaxTokens/ThinkingBudget-&gt;maxOutputTokens
/// translation, and the documented <c>IList&lt;Content&gt;</c> coverage gap.
/// </summary>
public class GeminiServiceCostGovernanceTests
{
    private const string GenerateJson =
        """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":42,"candidatesTokenCount":2,"totalTokenCount":44}}""";

    private const string CountTokensJson = """{"totalTokens":42}""";

    private static readonly FileObject TestImage = new(new byte[] { 137, 80, 78, 71, 0, 0, 0, 0 }, "test.png");

    private static (GeminiService Service, FakeHttpMessageHandler Handler, FakeCostGovernor Governor) CreateService(
        bool hasRequestCeiling)
    {
        // Routes on the endpoint path so both the pre-flight countTokens call and the real
        // generate/stream call can be served by the same fake handler with distinct, realistic bodies.
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.ToString();
            var json = path.Contains(":countTokens", StringComparison.Ordinal) ? CountTokensJson : GenerateJson;
            return FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, json);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(
            httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345",
            DefaultModel = "gemini-2.5-flash",
        });
        var governor = new FakeCostGovernor { HasRequestCeiling = hasRequestCeiling };
        var service = new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), governor);
        return (service, handler, governor);
    }

    [Fact]
    public async Task GenerateAsync_WhenNoCeilingConfigured_SkipsCountTokens_SendsOnlyTheGenerateRequest()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: false);

        await service.GenerateAsync("hello");

        Assert.Empty(governor.EstimateCalls); // never even asked
        var request = Assert.Single(handler.Requests);
        Assert.Contains(":generateContent", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateAsync_WhenCeilingConfigured_CountsTokensFirst_ThenSendsGenerateRequest()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);

        await service.GenerateAsync("hello");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(":countTokens", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains(":generateContent", handler.Requests[1].RequestUri!.ToString());

        var call = Assert.Single(governor.EstimateCalls);
        Assert.Equal("gemini-2.5-flash", call.Model);
        Assert.Equal(42, call.InputTokens); // from the stubbed countTokens response
        Assert.Null(call.MaxOutputTokens); // MaxTokens wasn't set
    }

    [Fact]
    public async Task GenerateAsync_WhenEstimateExceedsCeiling_ThrowsUnwrapped_AndNeverSendsTheGenerateRequest()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);
        governor.ThrowOnEstimate = true;

        await Assert.ThrowsAsync<GeminiRequestCostExceededException>(() => service.GenerateAsync("hello"));

        // Only the countTokens pre-flight call went out; the actual (billed) generate call never did.
        var request = Assert.Single(handler.Requests);
        Assert.Contains(":countTokens", request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(null, null, null)] // no MaxTokens set -> unbounded, output side not estimated
    [InlineData(100, null, 100)] // MaxTokens alone
    [InlineData(100, 50, 150)] // MaxTokens + positive ThinkingBudget: both bill as output, so they add
    [InlineData(100, 0, 100)] // ThinkingBudget == 0 (disabled): contributes nothing
    [InlineData(100, -1, 100)] // ThinkingBudget == -1 (model decides, unbounded): excluded, not misread
    public async Task GenerateAsync_ResolvesMaxOutputTokens_FromMaxTokensAndThinkingBudget(
        int? maxTokens, int? thinkingBudget, int? expectedMaxOutputTokens)
    {
        var (service, _, governor) = CreateService(hasRequestCeiling: true);

        await service.GenerateAsync("hello", new GeminiRequestOptions { MaxTokens = maxTokens, ThinkingBudget = thinkingBudget });

        var call = Assert.Single(governor.EstimateCalls);
        Assert.Equal(expectedMaxOutputTokens, call.MaxOutputTokens);
    }

    [Fact]
    public async Task GenerateWithImageAsync_WhenCeilingConfigured_UsesCountTokensWithImage()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);

        await service.GenerateWithImageAsync("describe this", TestImage);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(":countTokens", handler.Requests[0].RequestUri!.ToString());
        Assert.Single(governor.EstimateCalls);
    }

    [Fact]
    public async Task ChatAsync_MessageArray_WhenCeilingConfigured_UsesCountTokensChat()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);

        await service.ChatAsync([new MessageObject("user", "hi")]);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(":countTokens", handler.Requests[0].RequestUri!.ToString());
        Assert.Single(governor.EstimateCalls);
    }

    [Fact]
    public async Task ChatAsync_ContentList_NeverChecksEstimate_EvenWhenCeilingConfigured()
    {
        // Documented gap: there's no CountTokensAsync overload for a raw IList<Content>, so this
        // overload must skip the estimate entirely rather than silently misestimate or throw.
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);
        var contents = new List<Content> { new() { Role = "user", Parts = [new Part { Text = "hi" }] } };

        await service.ChatAsync(contents);

        Assert.Empty(governor.EstimateCalls);
        var request = Assert.Single(handler.Requests);
        Assert.Contains(":generateContent", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task StreamAsync_WhenEstimateExceedsCeiling_ThrowsBeforeAnyChunkYielded_AndNeverStreams()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);
        governor.ThrowOnEstimate = true;

        await Assert.ThrowsAsync<GeminiRequestCostExceededException>(async () =>
        {
            await foreach (var _ in service.StreamAsync("hello"))
            {
                Assert.Fail("No chunk should ever be yielded when the pre-flight estimate rejects the call.");
            }
        });

        var request = Assert.Single(handler.Requests);
        Assert.Contains(":countTokens", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task StreamChatAsync_ContentList_NeverChecksEstimate_EvenWhenCeilingConfigured()
    {
        var (service, handler, governor) = CreateService(hasRequestCeiling: true);
        var contents = new List<Content> { new() { Role = "user", Parts = [new Part { Text = "hi" }] } };

        await foreach (var _ in service.StreamChatAsync(contents))
        {
        }

        Assert.Empty(governor.EstimateCalls);
        var request = Assert.Single(handler.Requests);
        Assert.Contains(":streamGenerateContent", request.RequestUri!.ToString());
    }

    private sealed class FakeCostGovernor : ICostGovernor
    {
        public bool HasRequestCeiling { get; set; }
        public bool ThrowOnEstimate { get; set; }
        public List<(string? Model, int InputTokens, int? MaxOutputTokens)> EstimateCalls { get; } = [];

        public void CheckBudget()
        {
        }

        public decimal RecordSpend(string? model, UsageMetadata usage) => 0m;

        public decimal GetTodaySpend() => 0m;

        public decimal CheckEstimatedRequestCost(string? model, int inputTokens, int? maxOutputTokens)
        {
            EstimateCalls.Add((model, inputTokens, maxOutputTokens));
            if (ThrowOnEstimate)
            {
                throw new GeminiRequestCostExceededException("estimate exceeded ceiling", estimatedCostUsd: 99m, maxCostPerRequestUsd: 1m);
            }
            return 0m;
        }
    }
}
