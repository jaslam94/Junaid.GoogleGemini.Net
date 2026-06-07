using System.Net;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Interfaces;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

/// <summary>
/// End-to-end proof that resilience works through the real DI-configured HttpClient pipeline.
/// This is the regression guard for the original bug, where retrying reused a disposed HttpContent
/// and threw on the second attempt.
/// </summary>
public class GeminiResilienceTests
{
    private const string SuccessJson =
        """{"candidates":[{"content":{"role":"model","parts":[{"text":"ok"}]},"finishReason":"STOP"}]}""";

    [Fact]
    public async Task PostAsync_RetriesTransientFailures_ThenSucceeds()
    {
        // Returns 503, 503, then 200 — so success requires two retries of a POST with a body.
        var fake = FakeHttpMessageHandler.Sequence(
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.ServiceUnavailable, "{}"),
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.ServiceUnavailable, "{}"),
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, SuccessJson));

        var client = BuildClientWithPrimaryHandler(fake, maxRetries: 3);

        var response = await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-2.5-pro:generateContent", new GenerateContentRequest());

        Assert.Equal("ok", response.Text());
        Assert.Equal(3, fake.CallCount); // 2 failed attempts + 1 success == the request was re-sent
    }

    [Fact]
    public async Task PostAsync_GivesUpAfterMaxRetries_AndThrowsApiException()
    {
        var fake = FakeHttpMessageHandler.RespondWith(HttpStatusCode.ServiceUnavailable, "{}");
        var client = BuildClientWithPrimaryHandler(fake, maxRetries: 2);

        await Assert.ThrowsAsync<Exceptions.GeminiApiException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));

        Assert.Equal(3, fake.CallCount); // initial attempt + 2 retries
    }

    private static IGeminiClient BuildClientWithPrimaryHandler(FakeHttpMessageHandler fake, int maxRetries)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.MaxRetries = maxRetries;
            o.RetryBaseDelay = TimeSpan.Zero; // keep the test fast
            o.RateLimit.Enabled = false;
        });

        // Replace the primary handler with our fake (the last registration wins).
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => fake);

        return services.BuildServiceProvider().GetRequiredService<IGeminiClient>();
    }
}
