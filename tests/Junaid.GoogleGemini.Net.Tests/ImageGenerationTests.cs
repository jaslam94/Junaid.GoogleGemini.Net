using System.Net;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

public class ImageGenerationTests
{
    [Fact]
    public void RequestFactory_NoImageOptions_OmitsResponseModalitiesAndImageConfig()
    {
        var request = RequestFactory.CreateTextRequest("a cat", options: null);
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.DoesNotContain("responseModalities", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("imageConfig", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestFactory_WithResponseModalities_Serializes()
    {
        var request = RequestFactory.CreateTextRequest("a cat",
            new GeminiRequestOptions
            {
                ResponseModalities = [GeminiConstants.ResponseModalities.Text, GeminiConstants.ResponseModalities.Image]
            });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"responseModalities\":[\"TEXT\",\"IMAGE\"]", json);
    }

    [Fact]
    public void RequestFactory_WithImageAspectRatioAndSize_SerializesImageConfig()
    {
        var request = RequestFactory.CreateTextRequest("a cat",
            new GeminiRequestOptions
            {
                ImageAspectRatio = GeminiConstants.ImageAspectRatios.Widescreen16x9,
                ImageSize = GeminiConstants.ImageSizes.TwoK
            });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"imageConfig\":{\"aspectRatio\":\"16:9\",\"imageSize\":\"2K\"}", json);
    }

    [Fact]
    public void RequestFactory_OnlyAspectRatioSet_StillBuildsImageConfig()
    {
        var request = RequestFactory.CreateTextRequest("a cat",
            new GeminiRequestOptions { ImageAspectRatio = GeminiConstants.ImageAspectRatios.Square });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"imageConfig\":{\"aspectRatio\":\"1:1\"}", json);
    }

    [Fact]
    public void RequestFactory_OnlyImageSizeSet_StillBuildsImageConfig()
    {
        var request = RequestFactory.CreateTextRequest("a cat",
            new GeminiRequestOptions { ImageSize = GeminiConstants.ImageSizes.FourK });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("\"imageConfig\":{\"imageSize\":\"4K\"}", json);
    }

    [Fact]
    public async Task GenerateImageAsync_WhenModelAndModalitiesUnset_AppliesDefaults()
    {
        const string ok = """{"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"image/png","data":"iVBORw0KGgo="}}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var response = await service.GenerateImageAsync("a cat wearing a hat");

        // The default image model was used in the URL...
        Assert.Contains(GeminiConstants.Models.RecommendedImage, handler.Requests[0].RequestUri!.ToString());

        // ...and TEXT+IMAGE modalities were sent, even though the caller didn't set them.
        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"responseModalities\":[\"TEXT\",\"IMAGE\"]", body);

        Assert.Single(response.Images());
        Assert.Equal("image/png", response.Images()[0].MimeType);
    }

    [Fact]
    public async Task GenerateImageAsync_CallerSetModelAndModalities_LeavesThemAlone()
    {
        const string ok = """{"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"image/png","data":"iVBORw0KGgo="}}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.GenerateImageAsync("a cat wearing a hat", new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini3ProImage,
            ResponseModalities = [GeminiConstants.ResponseModalities.Image]
        });

        Assert.Contains(GeminiConstants.Models.Gemini3ProImage, handler.Requests[0].RequestUri!.ToString());
        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"responseModalities\":[\"IMAGE\"]", body);
    }

    [Fact]
    public async Task GenerateImageAsync_WhenApiReturnsNoCandidates_ThrowsWithImageGenerationLabel()
    {
        // Regression test: GenerateImageAsync used to delegate straight to GenerateAsync, which
        // hardcodes the "text generation" operation label used in this exception message — confusing
        // when debugging a fully-blocked image prompt. It must report its own operation now.
        const string emptyResponse = """{"candidates":[]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, emptyResponse);
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateImageAsync("a cat wearing a hat"));

        Assert.Contains("image generation", ex.Message);
        Assert.DoesNotContain("text generation", ex.Message);
    }

    private static GeminiService CreateService(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345" });
        return new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
    }
}
