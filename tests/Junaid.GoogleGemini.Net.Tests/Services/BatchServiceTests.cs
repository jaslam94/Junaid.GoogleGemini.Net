using System.Net;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

public class BatchServiceTests
{
    // -- Create (inline) ----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_SendsBatchWrapperWithNoModelField_AndReturnsJob()
    {
        const string responseJson =
            """{"name":"batches/123","displayName":"my-job","state":"JOB_STATE_PENDING"}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, responseJson);
        var batch = BuildBatchService(handler);

        var requests = new List<InlinedBatchRequest>
        {
            new() { Request = new GenerateContentRequest { Contents = { new Content { Parts = { new Part { Text = "hi" } } } } } }
        };

        var job = await batch.CreateAsync("gemini-3.6-flash", requests, "my-job");

        Assert.Equal("batches/123", job.Name);
        Assert.Equal("JOB_STATE_PENDING", job.State);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("models/gemini-3.6-flash:batchGenerateContent", handler.Requests[0].RequestUri!.ToString());

        var sentBody = handler.RequestBodies[0]!;
        using var doc = JsonDocument.Parse(sentBody);
        var root = doc.RootElement;

        // The body must be wrapped in a top-level "batch" property...
        Assert.True(root.TryGetProperty("batch", out var batchElement));
        Assert.Equal("my-job", batchElement.GetProperty("displayName").GetString());
        Assert.True(batchElement.TryGetProperty("inputConfig", out _));

        // ...and must NOT include a model field anywhere at the top level (see PLAN-batch-api.md §2.3:
        // the model is expressed only in the URL, matching every other endpoint in this library).
        Assert.False(root.TryGetProperty("model", out _));
        Assert.False(batchElement.TryGetProperty("model", out _));
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyRequestList()
    {
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            batch.CreateAsync("gemini-3.6-flash", new List<InlinedBatchRequest>()));
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankModel()
    {
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"));
        var requests = new List<InlinedBatchRequest> { new() { Request = new GenerateContentRequest() } };

        await Assert.ThrowsAsync<ArgumentException>(() => batch.CreateAsync("  ", requests));
    }

    // -- Create (from an already-uploaded file) ------------------------------------------------

    [Fact]
    public async Task CreateFromFileAsync_NormalizesFileName_AndSendsFileNameInputConfig()
    {
        const string responseJson = """{"name":"batches/456","state":"JOB_STATE_PENDING"}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, responseJson);
        var batch = BuildBatchService(handler);

        var job = await batch.CreateFromFileAsync("gemini-3.6-flash", "abc123"); // no "files/" prefix

        Assert.Equal("batches/456", job.Name);

        using var doc = JsonDocument.Parse(handler.RequestBodies[0]!);
        var inputConfig = doc.RootElement.GetProperty("batch").GetProperty("inputConfig");
        Assert.Equal("files/abc123", inputConfig.GetProperty("fileName").GetString());
    }

    // -- Create (convenience: write + upload JSONL from an in-memory list) ---------------------

    [Fact]
    public async Task CreateFromRequestsFileAsync_UploadsJsonl_ThenCreatesFromFile()
    {
        // Files client: resumable-upload handshake (start -> finalize), same shape as FileServiceTests.
        var uploadStart = new HttpResponseMessage(HttpStatusCode.OK);
        uploadStart.Headers.TryAddWithoutValidation("X-Goog-Upload-URL", "https://example.test/upload/session-1");
        const string uploadFinalizeJson = """{"file":{"name":"files/uploaded-jsonl","mimeType":"jsonl"}}""";
        var filesHandler = FakeHttpMessageHandler.Sequence(
            uploadStart, FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, uploadFinalizeJson));

        const string createJson = """{"name":"batches/789","state":"JOB_STATE_PENDING"}""";
        var batchHandler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, createJson);

        var batch = BuildBatchService(batchHandler, filesHandler);

        var lines = new List<Junaid.GoogleGemini.Net.Models.GoogleApi.BatchRequestLine>
        {
            new() { Key = "req-1", Request = new GenerateContentRequest { Contents = { new Content { Parts = { new Part { Text = "one" } } } } } },
            new() { Key = "req-2", Request = new GenerateContentRequest { Contents = { new Content { Parts = { new Part { Text = "two" } } } } } },
        };

        var job = await batch.CreateFromRequestsFileAsync("gemini-3.6-flash", lines);

        Assert.Equal("batches/789", job.Name);

        // The uploaded content (captured on the finalize request) must be two JSON lines, not an array.
        var uploadedBody = filesHandler.RequestBodies[1]!;
        var jsonLines = uploadedBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, jsonLines.Length);
        Assert.Contains("req-1", jsonLines[0]);
        Assert.Contains("req-2", jsonLines[1]);

        // And the create call must reference the uploaded file, not carry the requests inline.
        using var doc = JsonDocument.Parse(batchHandler.RequestBodies[0]!);
        var inputConfig = doc.RootElement.GetProperty("batch").GetProperty("inputConfig");
        Assert.Equal("files/uploaded-jsonl", inputConfig.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task CreateFromRequestsFileAsync_RejectsEmptySequence()
    {
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            batch.CreateFromRequestsFileAsync("gemini-3.6-flash", Array.Empty<Junaid.GoogleGemini.Net.Models.GoogleApi.BatchRequestLine>()));
    }

    // -- Get / List -----------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_NormalizesBareId_AndReturnsJob()
    {
        const string json = """{"name":"batches/123","state":"JOB_STATE_RUNNING"}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var batch = BuildBatchService(handler);

        var job = await batch.GetAsync("123"); // bare id, no "batches/" prefix

        Assert.Equal("JOB_STATE_RUNNING", job.State);
        Assert.Contains("batches/123", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task ListAsync_AppliesPagingParams()
    {
        const string json = """{"batches":[{"name":"batches/1"},{"name":"batches/2"}],"nextPageToken":"tok"}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, json);
        var batch = BuildBatchService(handler);

        var page = await batch.ListAsync(pageSize: 10, pageToken: "prev");

        Assert.Equal(2, page.Batches!.Length);
        Assert.Equal("tok", page.NextPageToken);
        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("pageSize=10", url);
        Assert.Contains("pageToken=prev", url);
    }

    [Fact]
    public async Task ListAsync_NoPagingParams_OmitsQueryString()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"batches":[]}""");
        var batch = BuildBatchService(handler);

        await batch.ListAsync();

        Assert.DoesNotContain("?", handler.Requests[0].RequestUri!.ToString());
    }

    // -- Cancel / Delete --------------------------------------------------------------------------

    [Fact]
    public async Task CancelAsync_PostsToCancelEndpoint()
    {
        // Deliberately an odd/unpredictable body shape - CancelAsync must not care what comes back,
        // only whether the call succeeded (see BatchService.CancelAsync's comment on why).
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"unexpected":"shape"}""");
        var batch = BuildBatchService(handler);

        await batch.CancelAsync("batches/123");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("batches/123:cancel", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task DeleteAsync_SendsDelete()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var batch = BuildBatchService(handler);

        await batch.DeleteAsync("batches/123");

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
    }

    // -- Error mapping --------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_MapsErrorResponse_ToGeminiApiException()
    {
        const string errorJson = """{"error":{"code":404,"message":"Batch job not found","status":"NOT_FOUND"}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.NotFound, errorJson);
        var batch = BuildBatchService(handler);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => batch.GetAsync("batches/missing"));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Batch job not found", ex.Error?.Message);
    }

    // -- WaitUntilCompleteAsync -------------------------------------------------------------------

    [Fact]
    public async Task WaitUntilCompleteAsync_ReturnsImmediately_WhenAlreadyTerminal()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"name":"batches/1","state":"JOB_STATE_SUCCEEDED"}""");
        var batch = BuildBatchService(handler);

        var job = await batch.WaitUntilCompleteAsync("batches/1", pollInterval: TimeSpan.FromMilliseconds(1));

        Assert.Equal("JOB_STATE_SUCCEEDED", job.State);
        Assert.Equal(1, handler.CallCount); // no extra polling once already terminal
    }

    [Fact]
    public async Task WaitUntilCompleteAsync_PollsUntilTerminal()
    {
        // Each response is consumed (and disposed) at most once, so the sequence needs distinct
        // instances per poll rather than reusing one "pending" response twice.
        var handler = FakeHttpMessageHandler.Sequence(
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"name":"batches/1","state":"JOB_STATE_RUNNING"}"""),
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"name":"batches/1","state":"JOB_STATE_RUNNING"}"""),
            FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"name":"batches/1","state":"JOB_STATE_SUCCEEDED"}"""));
        var batch = BuildBatchService(handler);

        var job = await batch.WaitUntilCompleteAsync("batches/1", pollInterval: TimeSpan.FromMilliseconds(1));

        Assert.Equal("JOB_STATE_SUCCEEDED", job.State);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task WaitUntilCompleteAsync_ThrowsTimeout_WhenNeverTerminal()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"name":"batches/1","state":"JOB_STATE_RUNNING"}""");
        var batch = BuildBatchService(handler);

        await Assert.ThrowsAsync<GeminiTimeoutException>(() =>
            batch.WaitUntilCompleteAsync("batches/1", pollInterval: TimeSpan.FromMilliseconds(1), timeout: TimeSpan.FromMilliseconds(20)));
    }

    [Theory]
    [InlineData("JOB_STATE_SUCCEEDED", true)]
    [InlineData("JOB_STATE_FAILED", true)]
    [InlineData("JOB_STATE_CANCELLED", true)]
    [InlineData("JOB_STATE_EXPIRED", true)]
    [InlineData("BATCH_STATE_SUCCEEDED", true)] // whichever prefix the live API actually uses (§2.4)
    [InlineData("JOB_STATE_PENDING", false)]
    [InlineData("JOB_STATE_RUNNING", false)]
    [InlineData(null, false)]
    public void IsTerminalState_MatchesBySuffix_RegardlessOfPrefix(string? state, bool expected)
    {
        Assert.Equal(expected, BatchService.IsTerminalState(state));
    }

    // -- GetResultsAsync --------------------------------------------------------------------------

    [Fact]
    public async Task GetResultsAsync_ReturnsInlineResponses_WithoutDownloading()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"); // never called
        var batch = BuildBatchService(handler);

        var job = new BatchJob
        {
            Name = "batches/1",
            State = "JOB_STATE_SUCCEEDED",
            Output = new BatchJobDestination
            {
                InlinedResponses = new InlinedBatchResponseList
                {
                    InlinedResponses = new List<InlinedBatchResponse>
                    {
                        new() { Response = new GenerateContentResponse() }
                    }
                }
            }
        };

        var results = await batch.GetResultsAsync(job);

        Assert.Single(results);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetResultsAsync_DownloadsAndParsesJsonl_ForFileBasedOutput()
    {
        const string jsonl =
            "{\"key\":\"req-1\",\"response\":{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"hi\"}]}}]}}\r\n" +
            "{\"key\":\"req-2\",\"error\":{\"code\":400,\"message\":\"bad request\"}}\n" +
            "\n"; // trailing blank line must be tolerated

        var filesHandler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonl) });
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"), filesHandler);

        var job = new BatchJob
        {
            Name = "batches/1",
            State = "JOB_STATE_SUCCEEDED",
            Output = new BatchJobDestination { FileName = "files/results" }
        };

        var results = await batch.GetResultsAsync(job);

        Assert.Equal(2, results.Count);
        Assert.Equal("req-1", results[0].Key);
        Assert.NotNull(results[0].Response);
        Assert.Equal("req-2", results[1].Key);
        Assert.Equal(400, results[1].Error?.Code);
    }

    [Fact]
    public async Task GetResultsAsync_ThrowsSerializationException_OnMalformedLine()
    {
        var filesHandler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json\n") });
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"), filesHandler);

        var job = new BatchJob { Output = new BatchJobDestination { FileName = "files/results" } };

        await Assert.ThrowsAsync<GeminiSerializationException>(() => batch.GetResultsAsync(job));
    }

    [Fact]
    public async Task GetResultsAsync_ThrowsGeminiException_WhenJobHasNoOutputYet()
    {
        var batch = BuildBatchService(FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}"));
        var job = new BatchJob { Name = "batches/1", State = "JOB_STATE_PENDING", Output = null };

        await Assert.ThrowsAsync<GeminiException>(() => batch.GetResultsAsync(job));
    }

    // -- DI wiring --------------------------------------------------------------------------------

    private static IBatchService BuildBatchService(FakeHttpMessageHandler batchHandler, FakeHttpMessageHandler? filesHandler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o =>
        {
            o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345";
            o.BaseUrl = new Uri("https://example.test/v1beta/");
            o.RateLimit.Enabled = false;
        });

        services.AddHttpClient(GeminiHttpClients.Batches).ConfigurePrimaryHttpMessageHandler(() => batchHandler);
        if (filesHandler is not null)
        {
            services.AddHttpClient(GeminiHttpClients.Files).ConfigurePrimaryHttpMessageHandler(() => filesHandler);
        }

        return services.BuildServiceProvider().GetRequiredService<IBatchService>();
    }
}
