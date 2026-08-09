using System.Net;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

/// <summary>
/// Covers the bug fix from PLAN-cost-governance.md §3: the original
/// <c>CountTokensAsync(prompt, ct)</c> always resolved the endpoint against
/// <see cref="GeminiOptions.DefaultModel"/>, ignoring any per-request model override. The new
/// <c>CountTokensAsync(prompt, options, ct)</c> overload must resolve against <c>options.Model</c>.
/// </summary>
public class CountTokensModelOverloadTests
{
    private static (GeminiService Service, FakeHttpMessageHandler Handler) CreateService()
    {
        const string json = """{"totalTokens":5}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(
            httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345",
            DefaultModel = "gemini-2.5-flash",
        });
        var service = new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
        return (service, handler);
    }

    [Fact]
    public async Task CountTokensAsync_WithModelOverride_ResolvesEndpointAgainstThatModel_NotDefaultModel()
    {
        var (service, handler) = CreateService();

        await service.CountTokensAsync("hello", new GeminiRequestOptions { Model = "gemini-3.1-pro-preview" });

        var requestUri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Contains("gemini-3.1-pro-preview", requestUri.ToString());
        Assert.DoesNotContain("gemini-2.5-flash", requestUri.ToString());
    }

    [Fact]
    public async Task CountTokensAsync_WithoutOptions_FallsBackToDefaultModel()
    {
        var (service, handler) = CreateService();

        await service.CountTokensAsync("hello");

        var requestUri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Contains("gemini-2.5-flash", requestUri.ToString());
    }

    [Fact]
    public async Task CountTokensAsync_WithOptionsButNoModel_FallsBackToDefaultModel()
    {
        var (service, handler) = CreateService();

        await service.CountTokensAsync("hello", new GeminiRequestOptions());

        var requestUri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Contains("gemini-2.5-flash", requestUri.ToString());
    }
}
