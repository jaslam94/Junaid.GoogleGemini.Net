using System.Net;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Xunit;

namespace Junaid.GoogleGemini.Net.Testing.Tests;

public class CassetteHandlerTests : IDisposable
{
    private readonly string _cassettePath =
        Path.Combine(Path.GetTempPath(), $"cassette-{Guid.NewGuid():N}.json");

    private const string Endpoint = "models/test-model:generateContent";

    private static string ResponseJson(string text) =>
        $$"""{"candidates":[{"content":{"role":"model","parts":[{"text":"{{text}}"}]},"finishReason":"STOP","index":0}]}""";

    public void Dispose()
    {
        if (File.Exists(_cassettePath))
        {
            File.Delete(_cassettePath);
        }
    }

    [Fact]
    public async Task RecordOnce_FirstRun_CallsLiveApi_AndWritesCassette_WithoutTheApiKey()
    {
        var inner = FakeInnerHandler.Json(HttpStatusCode.OK, ResponseJson("Hello!"));
        var client = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.RecordOnce, inner);

        var response = await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        Assert.Equal("Hello!", response.Text());
        Assert.Equal(1, inner.CallCount);
        Assert.True(File.Exists(_cassettePath));

        var cassetteText = await File.ReadAllTextAsync(_cassettePath);
        Assert.DoesNotContain(CassetteTestHarness.ApiKey, cassetteText);
        Assert.Contains("Hello!", cassetteText);
    }

    [Fact]
    public async Task RecordOnce_SecondRun_ReplaysFromCassette_NeverReachesLiveApi()
    {
        var recorder = FakeInnerHandler.Json(HttpStatusCode.OK, ResponseJson("Hello!"));
        var recordingClient = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.RecordOnce, recorder);
        await recordingClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        // A fresh "session": new handler instance, new fake that fails the test if it's ever hit.
        var mustNotBeCalled = FakeInnerHandler.MustNotBeCalled();
        var replayClient = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.RecordOnce, mustNotBeCalled);

        var response = await replayClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        Assert.Equal("Hello!", response.Text());
        Assert.Equal(0, mustNotBeCalled.CallCount);
    }

    [Fact]
    public async Task Replay_NoMatchingInteraction_ThrowsCassetteException()
    {
        var client = CassetteTestHarness.BuildClient(
            _cassettePath, CassetteMode.Replay, FakeInnerHandler.MustNotBeCalled());

        await Assert.ThrowsAsync<CassetteException>(() =>
            client.PostAsync<GenerateContentRequest, GenerateContentResponse>(Endpoint, new GenerateContentRequest()));
    }

    [Fact]
    public async Task Replay_MatchesRequestBody_IgnoringJsonFormattingDifferences()
    {
        // A new GenerateContentRequest() serializes (via GeminiJson.Default, nulls omitted) to the
        // compact `{"contents":[]}`. This cassette spells the same JSON with extra whitespace and
        // different formatting, to prove matching is JSON-semantic rather than a textual
        // comparison. Built via the internal model (not hand-typed JSON text) so the test can't
        // itself introduce an escaping bug.
        var cassette = new CassetteFile
        {
            Interactions =
            [
                new CassetteInteraction
                {
                    Request = new CassetteRequest
                    {
                        Method = "POST",
                        Uri = $"/v1beta/{Endpoint}",
                        Body = "{\n  \"contents\":\n  [ ]\n}",
                    },
                    Response = new CassetteResponse
                    {
                        StatusCode = 200,
                        ContentType = "application/json",
                        Body = ResponseJson("from cassette"),
                    },
                },
            ],
        };
        await CassetteStore.SaveAsync(_cassettePath, cassette, CancellationToken.None);

        var client = CassetteTestHarness.BuildClient(
            _cassettePath, CassetteMode.Replay, FakeInnerHandler.MustNotBeCalled());

        var response = await client.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        Assert.Equal("from cassette", response.Text());
    }

    [Fact]
    public async Task RepeatedIdenticalRequests_ReplayInRecordedOrder()
    {
        var recorder = new FakeInnerHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson("first"), System.Text.Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson("second"), System.Text.Encoding.UTF8, "application/json"),
            });
        var recordingClient = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.Record, recorder);

        await recordingClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());
        await recordingClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        var replayClient = CassetteTestHarness.BuildClient(
            _cassettePath, CassetteMode.Replay, FakeInnerHandler.MustNotBeCalled());

        var first = await replayClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());
        var second = await replayClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        Assert.Equal("first", first.Text());
        Assert.Equal("second", second.Text());
    }

    [Fact]
    public async Task Record_AlwaysCallsLiveApi_EvenWhenACassetteAlreadyExists()
    {
        var first = FakeInnerHandler.Json(HttpStatusCode.OK, ResponseJson("v1"));
        var firstClient = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.Record, first);
        await firstClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        var second = FakeInnerHandler.Json(HttpStatusCode.OK, ResponseJson("v2"));
        var secondClient = CassetteTestHarness.BuildClient(_cassettePath, CassetteMode.Record, second);
        var response = await secondClient.PostAsync<GenerateContentRequest, GenerateContentResponse>(
            Endpoint, new GenerateContentRequest());

        Assert.Equal("v2", response.Text());
        Assert.Equal(1, second.CallCount);
    }
}
