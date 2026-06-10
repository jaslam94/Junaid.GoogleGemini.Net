using System.Diagnostics;
using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Telemetry;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Infrastructure;

public class GeminiClientTests
{
    private static GeminiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        return new GeminiClient(
            httpClient,
            NullLogger<GeminiClient>.Instance,
            GeminiRateLimiter.CreateDisabled());
    }

    [Fact]
    public async Task PostAsync_WhenApiReturnsCandidates_DeserializesText()
    {
        const string json = """
        {"candidates":[{"content":{"role":"model","parts":[{"text":"Hello!"}]},"finishReason":"STOP","index":0}]}
        """;
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var response = await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-2.5-pro:generateContent", new GenerateContentRequest());

        Assert.Equal("Hello!", response.Text());
        Assert.Single(handler.Requests);
        Assert.True(handler.Requests[0].Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task PostAsync_WhenApiReturnsError_ThrowsGeminiExceptionWithStatus()
    {
        const string json = """{"error":{"code":400,"message":"Invalid request","status":"INVALID_ARGUMENT"}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.BadRequest, json);
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("INVALID_ARGUMENT", ex.Status);
        Assert.Contains("Invalid request", ex.Message);
        // A 400 is a client error and must NOT be retried.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PostAsync_WhenSuccessBodyIsInvalidJson_ThrowsSerializationException()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "this is not json");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<GeminiSerializationException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));
    }

    [Fact]
    public async Task PostAsync_WhenSendTimesOut_ThrowsTimeoutException()
    {
        // A timeout surfaces as a TaskCanceledException while the caller's token is NOT cancelled.
        var handler = new FakeHttpMessageHandler((_, _, _) =>
            throw new TaskCanceledException("simulated timeout"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<GeminiTimeoutException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
                "models/x:generateContent", new GenerateContentRequest()));
    }

    [Fact]
    public async Task StreamAsync_ParsesSseEventsIntoChunks()
    {
        const string sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Hello \"}]}}]}\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"world\"}]}}]}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var client = CreateClient(handler);

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamAsync(
            "models/x:streamGenerateContent?alt=sse", new GenerateContentRequest()))
        {
            chunks.Add(chunk.Text());
        }

        Assert.Equal(["Hello ", "world"], chunks);
    }

    [Fact]
    public async Task StreamAsync_SkipsMalformedEvents()
    {
        const string sse =
            "data: this-is-not-json\n" +
            "\n" +
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}\n" +
            "\n";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, sse);
        var client = CreateClient(handler);

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamAsync(
            "models/x:streamGenerateContent?alt=sse", new GenerateContentRequest()))
        {
            chunks.Add(chunk.Text());
        }

        Assert.Equal(["ok"], chunks);
    }

    [Fact]
    public async Task PostAsync_EmitsActivity_WithGenAiTags()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GeminiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        const string json =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":2,"totalTokenCount":7}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        // A unique model name keeps this assertion isolated from activities that other test classes
        // emit in parallel onto the same (process-global) ActivitySource.
        await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            "models/gemini-activity-probe:generateContent", new GenerateContentRequest());

        var activity = Assert.Single(activities, a => a.DisplayName == "generateContent gemini-activity-probe");
        Assert.Equal("gemini", activity.GetTagItem("gen_ai.system"));
        Assert.Equal("gemini-activity-probe", activity.GetTagItem("gen_ai.request.model"));
        Assert.Equal(5, activity.GetTagItem("gen_ai.usage.input_tokens"));
    }
}
