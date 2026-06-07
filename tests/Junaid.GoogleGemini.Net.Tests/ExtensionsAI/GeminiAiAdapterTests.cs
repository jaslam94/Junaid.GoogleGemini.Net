using System.Net;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Extensions.AI;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.ExtensionsAI;

public class GeminiAiAdapterTests
{
    [Fact]
    public async Task ChatClient_GetResponseAsync_ReturnsAssistantText()
    {
        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"Hi from Gemini"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":4,"totalTokenCount":7}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var provider = BuildProvider(handler, services => services.AddGeminiChatClient("gemini-2.5-flash"));

        var chat = provider.GetRequiredService<IChatClient>();
        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.Contains("Hi from Gemini", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(7, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task EmbeddingGenerator_GenerateAsync_ReturnsVector()
    {
        var values = string.Join(",", Enumerable.Repeat("0.1", 64));
        var json = "{\"embedding\":{\"values\":[" + values + "]}}";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var provider = BuildProvider(handler,
            services => services.AddGeminiEmbeddingGenerator("gemini-embedding-001"));

        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var result = await generator.GenerateAsync(["hello world"]);

        Assert.Single(result);
        Assert.Equal(64, result[0].Vector.Length);
    }

    private static ServiceProvider BuildProvider(FakeHttpMessageHandler handler, Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.RateLimit.Enabled = false;
        });
        services.AddHttpClient<GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        register(services);
        return services.BuildServiceProvider();
    }
}
