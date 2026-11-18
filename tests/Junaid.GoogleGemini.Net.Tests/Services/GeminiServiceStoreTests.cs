using System.Net;
using System.Text.Json;
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
/// The <c>store</c> request field. Since 2026-10-26, Google stores <c>generateContent</c> requests by
/// default for new paid projects, and <c>store: false</c> opts one request out. The option must reach
/// the wire on every path that builds a request, and an unset option must send nothing.
/// Four <c>RequestFactory</c> methods build a request, so each has its own test through the public
/// service method that reaches it.
/// </summary>
public class GeminiServiceStoreTests
{
    private const string OkResponse =
        """{"candidates":[{"content":{"role":"model","parts":[{"text":"OK"}]},"finishReason":"STOP"}]}""";

    // A tiny PNG header. The file validator checks the magic bytes only.
    private static readonly FileObject TestImage = new(new byte[] { 137, 80, 78, 71, 0, 0, 0, 0 }, "test.png");

    private static (GeminiService Service, FakeHttpMessageHandler Handler) CreateService(string responseJson = OkResponse)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, responseJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345",
            DefaultModel = "gemini-4-hydrogen"
        });
        var service = new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
        return (service, handler);
    }

    // The value of the top-level "store" key in the one request body that the handler saw.
    // Returns null when the key is absent.
    private static JsonElement? SentStore(FakeHttpMessageHandler handler)
    {
        Assert.Single(handler.RequestBodies);
        using var doc = JsonDocument.Parse(handler.RequestBodies[0]!);
        return doc.RootElement.TryGetProperty("store", out var store) ? store.Clone() : null;
    }

    private static void AssertSentFalse(FakeHttpMessageHandler handler)
    {
        var store = SentStore(handler);
        Assert.NotNull(store);
        Assert.Equal(JsonValueKind.False, store!.Value.ValueKind);
    }

    private static void AssertNothingSent(FakeHttpMessageHandler handler) => Assert.Null(SentStore(handler));

    private static readonly IList<Content> OneContent =
        [new Content { Role = "user", Parts = [new Part { Text = "Reply with the single word OK." }] }];

    private static readonly MessageObject[] OneMessage = [new MessageObject("user", "Reply with the single word OK.")];

    // ---- The body: false, true and unset -------------------------------------------------------

    [Fact]
    public async Task StoreFalse_IsSentAsFalse()
    {
        var (service, handler) = CreateService();

        await service.GenerateAsync("hi", new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    [Fact]
    public async Task StoreTrue_IsSentAsTrue()
    {
        var (service, handler) = CreateService();

        await service.GenerateAsync("hi", new GeminiRequestOptions { Store = true });

        var store = SentStore(handler);
        Assert.NotNull(store);
        Assert.Equal(JsonValueKind.True, store!.Value.ValueKind);
    }

    [Fact]
    public async Task StoreNull_SendsNoStoreKey()
    {
        var (service, handler) = CreateService();

        await service.GenerateAsync("hi", new GeminiRequestOptions { Store = null });

        AssertNothingSent(handler);
        Assert.DoesNotContain("\"store\"", handler.RequestBodies[0]!);
    }

    [Fact]
    public async Task NoOptionsAtAll_SendsNoStoreKey()
    {
        var (service, handler) = CreateService();

        await service.GenerateAsync("hi");

        Assert.DoesNotContain("\"store\"", handler.RequestBodies[0]!);
    }

    // ---- One test per RequestFactory method, through the public method that reaches it ---------

    [Fact]
    public async Task GenerateAsync_Text_SendsStore()                       // RequestFactory.CreateTextRequest
    {
        var (service, handler) = CreateService();

        await service.GenerateAsync("hi", new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    [Fact]
    public async Task GenerateWithImageAsync_SendsStore()                   // RequestFactory.CreateVisionRequest
    {
        var (service, handler) = CreateService();

        await service.GenerateWithImageAsync("what is this?", TestImage, new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    [Fact]
    public async Task ChatAsync_Messages_SendsStore()                       // RequestFactory.CreateChatRequest
    {
        var (service, handler) = CreateService();

        await service.ChatAsync(OneMessage, new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    [Fact]
    public async Task ChatAsync_Contents_SendsStore()                       // RequestFactory.CreateRequest
    {
        var (service, handler) = CreateService();

        await service.ChatAsync(OneContent, new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    // ---- Paths that copy the options with Clone() ----------------------------------------------

    [Fact]
    public async Task GenerateAsyncOfT_KeepsStore_ThroughClone()
    {
        // GenerateAsync<T> clones the options before it adds the JSON schema.
        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"{\"name\":\"Ada\"}"}]},"finishReason":"STOP"}]}""";
        var (service, handler) = CreateService(json);

        await service.GenerateAsync<Person>("Give me a name.", new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public async Task GenerateImageAsync_KeepsStore_ThroughClone()
    {
        var (service, handler) = CreateService();

        await service.GenerateImageAsync("a cat", new GeminiRequestOptions { Store = false });

        AssertSentFalse(handler);
    }

    [Fact]
    public void Clone_CopiesStore()
    {
        Assert.False(new GeminiRequestOptions { Store = false }.Clone().Store);
        Assert.True(new GeminiRequestOptions { Store = true }.Clone().Store);
        Assert.Null(new GeminiRequestOptions().Clone().Store);
    }

    [Fact]
    public void UseModel_KeepsStore()
    {
        var copy = new GeminiRequestOptions { Store = false }.UseModel("gemini-4-carbon");

        Assert.False(copy.Store);
        Assert.Equal("gemini-4-carbon", copy.Model);
    }
}
