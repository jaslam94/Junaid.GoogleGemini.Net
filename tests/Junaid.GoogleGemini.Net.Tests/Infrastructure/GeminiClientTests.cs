using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
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
}
