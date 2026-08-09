using System.Net;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

public class CachingServiceTests
{
    [Fact]
    public async Task CreateAsync_PostsCachedContent_AndReturnsIt()
    {
        const string json =
            """{"name":"cachedContents/xyz","model":"models/gemini-2.5-flash","usageMetadata":{"totalTokenCount":1024}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var client = new GeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") },
            NullLogger<GeminiClient>.Instance,
            GeminiRateLimiter.CreateDisabled(),
            GeminiCostGovernor.CreateDisabled());
        var caching = new CachingService(client, NullLogger<CachingService>.Instance);

        var created = await caching.CreateAsync(new CachedContent
        {
            Model = "models/gemini-2.5-flash",
            Contents = [new Content { Role = "user", Parts = [new Part { Text = "big reusable context" }] }],
            Ttl = "300s"
        });

        Assert.Equal("cachedContents/xyz", created.Name);
        Assert.Equal(1024, created.UsageMetadata!.TotalTokenCount);

        var requestBody = handler.RequestBodies[0]!;
        Assert.Contains("\"model\":\"models/gemini-2.5-flash\"", requestBody);
        Assert.Contains("\"ttl\":\"300s\"", requestBody);
    }

    [Fact]
    public void RequestFactory_ReferencesCachedContent()
    {
        var request = RequestFactory.CreateTextRequest("hi", new GeminiRequestOptions
        {
            CachedContent = "cachedContents/xyz"
        });

        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"cachedContent\":\"cachedContents/xyz\"", json);
    }
}
