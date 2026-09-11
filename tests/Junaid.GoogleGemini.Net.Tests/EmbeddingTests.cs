using System.Net;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

/// <summary>
/// Unit tests for multimodal embeddings input (<c>gemini-embedding-2</c>). No unit test coverage
/// existed for <see cref="EmbeddingService"/> at all before this change; this file covers both the
/// pre-existing text-only methods and the new multimodal ones. See each test's comment for the
/// corresponding section of <c>PLAN-embeddings-multimodal.md</c>.
/// </summary>
public class EmbeddingTests
{
    [Fact]
    public async Task EmbedContentAsync_Text_SendsTextOnlyContent()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.EmbedContentAsync("gemini-embedding-2", "hello");

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"text\":\"hello\"", body);
        Assert.DoesNotContain("inlineData", body);
        Assert.DoesNotContain("fileData", body);
    }

    // PLAN-embeddings-multimodal.md §2: the request shape live-confirmed for image input.
    [Fact]
    public async Task EmbedContentAsync_Media_SendsTextAndInlineDataParts()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.EmbedContentAsync("gemini-embedding-2", [1, 2, 3], "image/jpeg", "A red circle");

        var body = handler.RequestBodies[0]!;
        Assert.EndsWith("models/gemini-embedding-2:embedContent", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("\"text\":\"A red circle\"", body);
        Assert.Contains("\"mimeType\":\"image/jpeg\"", body);
        Assert.Contains("\"data\":\"AQID\"", body); // base64 of [1,2,3]
    }

    [Fact]
    public async Task EmbedContentAsync_MediaWithoutText_OmitsTextPart()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.EmbedContentAsync("gemini-embedding-2", [1, 2, 3], "audio/wav");

        var body = handler.RequestBodies[0]!;
        Assert.DoesNotContain("\"text\"", body);
        Assert.Contains("\"mimeType\":\"audio/wav\"", body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public async Task EmbedContentAsync_MissingMedia_Throws(byte[]? mediaBytes)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EmbedContentAsync("gemini-embedding-2", mediaBytes!, "image/jpeg"));
        Assert.Empty(handler.Requests);
    }

    // PLAN-embeddings-multimodal.md §2: the fileData shape live-confirmed to work identically to inline data.
    [Fact]
    public async Task EmbedFileAsync_SendsFileDataNotInlineData()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.EmbedFileAsync(
            "gemini-embedding-2", "https://generativelanguage.googleapis.com/v1beta/files/abc", "video/mp4", "A summary clip");

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"fileUri\":\"https://generativelanguage.googleapis.com/v1beta/files/abc\"", body);
        Assert.Contains("\"mimeType\":\"video/mp4\"", body);
        Assert.DoesNotContain("\"data\"", body);
    }

    [Fact]
    public async Task EmbedFileAsync_MissingFileUriOrMimeType_ThrowsWithoutNetworkCall()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EmbedFileAsync("gemini-embedding-2", "", "video/mp4"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EmbedFileAsync("gemini-embedding-2", "files/abc", ""));
        Assert.Empty(handler.Requests);
    }

    // PLAN-embeddings-multimodal.md §2: batchEmbedContents already accepts one Content per request,
    // each of which can mix text and media; confirmed live it returns one separate embedding per input.
    [Fact]
    public async Task BatchEmbedContentAsync_Inputs_SendsOneRequestPerInputInOrder()
    {
        var ok = FakeBatchEmbeddingJson(50, 50);
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var inputs = new List<EmbeddingInput>
        {
            new() { Text = "plain text entry" },
            new() { Text = "A red circle", MediaBytes = [1, 2, 3], MimeType = "image/jpeg" },
        };

        var response = await service.BatchEmbedContentAsync("gemini-embedding-2", inputs);

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"text\":\"plain text entry\"", body);
        Assert.Contains("\"text\":\"A red circle\"", body);
        Assert.Contains("\"mimeType\":\"image/jpeg\"", body);
        Assert.Equal(2, response.Embeddings!.Length);
    }

    [Fact]
    public async Task BatchEmbedContentAsync_InputWithNeitherTextNorMedia_ThrowsWithoutNetworkCall()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);
        var inputs = new List<EmbeddingInput> { new() };

        await Assert.ThrowsAsync<ArgumentException>(() => service.BatchEmbedContentAsync("gemini-embedding-2", inputs));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task BatchEmbedContentAsync_InputWithTextOnly_Succeeds()
    {
        var ok = FakeBatchEmbeddingJson(50);
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);
        var inputs = new List<EmbeddingInput> { new() { Text = "text ok" } };

        var response = await service.BatchEmbedContentAsync("gemini-embedding-2", inputs);

        Assert.Single(response.Embeddings!);
    }

    [Fact]
    public async Task BatchEmbedContentAsync_InputWithBothMediaBytesAndFileUri_ThrowsWithoutNetworkCall()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);
        var inputs = new List<EmbeddingInput>
        {
            new() { MediaBytes = [1], FileUri = "files/abc", MimeType = "image/jpeg" },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.BatchEmbedContentAsync("gemini-embedding-2", inputs));
        Assert.Empty(handler.Requests);
    }

    // PLAN-embeddings-multimodal.md §2: TaskType is confirmed live to have zero effect on
    // gemini-embedding-2 (accepted, not rejected, silently ignored). This is the regression test for
    // the one-time warning that exists because of that finding.
    [Fact]
    public async Task EmbedContentAsync_TaskTypeWithGeminiEmbedding2_LogsOneTimeWarning()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var logger = new CapturingLogger<EmbeddingService>();
        var service = CreateService(handler, logger);
        var options = new EmbeddingOptions { TaskType = GeminiConstants.EmbeddingTaskTypes.RetrievalQuery };

        await service.EmbedContentAsync("gemini-embedding-2", "hello", options);
        await service.EmbedContentAsync("gemini-embedding-2", "world", options);

        var warnings = logger.Messages.Where(m => m.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings); // Logged once, not once per call.
        Assert.Contains("TaskType", warnings[0].Message);
        Assert.Contains("gemini-embedding-2", warnings[0].Message);
    }

    [Fact]
    public async Task EmbedContentAsync_TaskTypeWithOlderModel_DoesNotWarn()
    {
        var ok = FakeEmbeddingJson();
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var logger = new CapturingLogger<EmbeddingService>();
        var service = CreateService(handler, logger);
        var options = new EmbeddingOptions { TaskType = GeminiConstants.EmbeddingTaskTypes.RetrievalQuery };

        await service.EmbedContentAsync("gemini-embedding-001", "hello", options);

        Assert.DoesNotContain(logger.Messages, m => m.Level == LogLevel.Warning);
    }

    // EmbeddingService.ValidateEmbeddingResponse rejects anything under 50 dimensions as "unexpectedly
    // low" (a real, pre-existing sanity check, not something these tests should route around), so
    // every fake response here needs at least 50 values, not a handful of representative ones.
    private static string FakeEmbeddingJson(int dims = 50) =>
        "{\"embedding\":{\"values\":[" + string.Join(",", Enumerable.Repeat("0.1", dims)) + "]}}";

    private static string FakeBatchEmbeddingJson(params int[] dimsPerEmbedding)
    {
        var embeddings = dimsPerEmbedding.Select(d => "{\"values\":[" + string.Join(",", Enumerable.Repeat("0.1", d)) + "]}");
        return "{\"embeddings\":[" + string.Join(",", embeddings) + "]}";
    }

    private static EmbeddingService CreateService(FakeHttpMessageHandler handler, ILogger<EmbeddingService>? logger = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345" });
        return new EmbeddingService(client, logger ?? NullLogger<EmbeddingService>.Instance, options);
    }

    /// <summary>Minimal <see cref="ILogger{T}"/> fake that records formatted messages, for asserting
    /// on warnings without a real logging provider.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add((logLevel, formatter(state, exception)));
        }
    }
}
