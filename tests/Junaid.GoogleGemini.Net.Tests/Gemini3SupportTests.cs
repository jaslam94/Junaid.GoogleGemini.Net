using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

public class Gemini3SupportTests
{
    [Fact]
    public void ValidateModelName_AcceptsUnknownModels()
    {
        // Must not throw for a Gemini 3 model the library has never heard of.
        ValidationUtilities.ValidateModelName("gemini-3.1-pro-preview");
        ValidationUtilities.ValidateEmbeddingModel("gemini-embedding-2");
    }

    [Fact]
    public void ValidateModelName_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => ValidationUtilities.ValidateModelName("  "));
    }

    [Fact]
    public void RequestFactory_NoOptions_OmitsTemperature()
    {
        var request = RequestFactory.CreateTextRequest("hi", options: null);
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        // No forced default — let the model use its native temperature (1.0 on Gemini 3).
        Assert.DoesNotContain("temperature", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThinkingLevel_Serializes()
    {
        var request = RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { ThinkingLevel = GeminiConstants.ThinkingLevels.High });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"thinkingLevel\":\"high\"", json);
    }

    [Fact]
    public void SettingBothThinkingBudgetAndLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() => RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { ThinkingBudget = 256, ThinkingLevel = "high" }));
    }

    [Fact]
    public void MediaResolution_Serializes()
    {
        var request = RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { MediaResolution = GeminiConstants.MediaResolutions.High });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"mediaResolution\":\"media_resolution_high\"", json);
    }

    [Fact]
    public void ThoughtSignature_RoundTrips()
    {
        const string responseJson =
            """{"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"f","args":{}},"thoughtSignature":"sig-123"}]}}]}""";

        var response = JsonSerializer.Deserialize<GenerateContentResponse>(responseJson, GeminiJson.Default)!;
        var part = response.Candidates![0].Content!.Parts[0];
        Assert.Equal("sig-123", part.ThoughtSignature);

        // And it serializes back out when echoed into a follow-up request.
        var roundTripped = JsonSerializer.Serialize(response.Candidates[0].Content, GeminiJson.Default);
        Assert.Contains("\"thoughtSignature\":\"sig-123\"", roundTripped);
    }

    [Fact]
    public async Task ChatAsync_ContentOverload_SendsPartsVerbatim()
    {
        const string ok =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"done"}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = "weather?" }] },
            new()
            {
                Role = "model",
                Parts =
                [
                    new Part
                    {
                        FunctionCall = new FunctionCallPart { Name = "get_weather", Args = JsonNode.Parse("{\"city\":\"Paris\"}") },
                        ThoughtSignature = "sig-abc"
                    }
                ]
            },
            new()
            {
                Role = "user",
                Parts = [new Part { FunctionResponse = new FunctionResponsePart { Name = "get_weather", Response = JsonNode.Parse("{\"tempC\":18}") } }]
            },
        };

        var response = await service.ChatAsync(contents);

        Assert.Equal("done", response.Text());
        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"thoughtSignature\":\"sig-abc\"", body); // signature echoed back
        Assert.Contains("\"functionResponse\"", body);
        Assert.Contains("get_weather", body);
    }

    [Fact]
    public void DefaultTimeout_IsGenerousForThinkingModels()
    {
        // Gemini 3 thinking models can take >1 min; the old 30s default caused spurious timeouts.
        Assert.Equal(100, new GeminiOptions().TimeoutSeconds);
    }

    private static GeminiService CreateService(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345" });
        return new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
    }
}
