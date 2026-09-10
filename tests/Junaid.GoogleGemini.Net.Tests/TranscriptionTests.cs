using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

/// <summary>
/// Unit tests for speech-to-text (the Interactions API path, see <c>PLAN-stt.md</c>). Each test here
/// exists because it exercises a specific real behavior confirmed live before this code was written:
/// see the comment on each test for the corresponding section of <c>PLAN-stt.md</c>.
/// </summary>
public class TranscriptionTests
{
    [Fact]
    public async Task TranscribeAsync_InlineAudio_SendsCorrectRequestShape()
    {
        const string ok = """{"id":"v1_abc","status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"hello world"}]}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var response = await service.TranscribeAsync([1, 2, 3], "audio/wav");

        Assert.EndsWith("interactions", handler.Requests[0].RequestUri!.ToString());
        var body = handler.RequestBodies[0]!;
        Assert.Contains($"\"model\":\"{GeminiConstants.Models.Gemini35Transcribe}\"", body);
        Assert.Contains("\"type\":\"audio\"", body);
        Assert.Contains("\"data\":\"AQID\"", body); // base64 of [1,2,3]
        Assert.Contains("\"mime_type\":\"audio/wav\"", body);
        Assert.DoesNotContain("\"uri\"", body);
        Assert.Equal("hello world", response.Text());
    }

    [Fact]
    public async Task TranscribeFileAsync_SendsUriNotData()
    {
        const string ok = """{"id":"v1_abc","status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"hi"}]}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.TranscribeFileAsync("https://generativelanguage.googleapis.com/v1beta/files/abc", "audio/wav");

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"uri\":\"https://generativelanguage.googleapis.com/v1beta/files/abc\"", body);
        Assert.DoesNotContain("\"data\"", body);
    }

    [Fact]
    public async Task TranscribeAsync_WithTranscriptionOptions_SendsTranscriptionConfig()
    {
        const string ok = """{"id":"v1_abc","status":"completed","steps":[]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.TranscribeAsync([1], "audio/wav", new TranscriptionOptions
        {
            LanguageCodes = ["en-US"],
            DiarizationMode = "speaker",
            TimestampGranularities = ["word"],
        });

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"language_codes\":[\"en-US\"]", body);
        Assert.Contains("\"diarization_mode\":\"speaker\"", body);
        Assert.Contains("\"timestamp_granularities\":[\"word\"]", body);
    }

    // PLAN-stt.md §3.4: real, confirmed HTTP 400 ("custom_vocabulary is incompatible with
    // diarization") when these are combined. Caught client-side, before any network call.
    [Theory]
    [InlineData("speaker", null)]
    [InlineData(null, "word")]
    public async Task TranscribeAsync_CustomVocabularyWithDiarizationOrTimestamps_ThrowsWithoutNetworkCall(
        string? diarizationMode, string? timestampGranularity)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var options = new TranscriptionOptions
        {
            CustomVocabulary = ["Gemini"],
            DiarizationMode = diarizationMode,
            TimestampGranularities = timestampGranularity is null ? null : [timestampGranularity],
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.TranscribeAsync([1], "audio/wav", options));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void TranscriptionOptions_CustomVocabularyAlone_DoesNotThrow()
    {
        var options = new TranscriptionOptions { CustomVocabulary = ["Gemini"] };
        options.Validate(); // Should not throw.
    }

    // PLAN-stt.md §3.4: the Interactions API's error body has a STRING `code` (e.g.
    // "invalid_request"), unlike generateContent's numeric one. Reusing the generic error path
    // unmodified would throw an opaque GeminiSerializationException instead of surfacing the real
    // message; this is the regression test for that.
    [Fact]
    public async Task TranscribeAsync_ApiError_SurfacesRealMessageAndCode_NotASerializationException()
    {
        const string errorJson = """{"error":{"message":"custom_vocabulary is incompatible with diarization.","code":"invalid_request"}}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.BadRequest, errorJson);
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(
            () => service.TranscribeAsync([1], "audio/wav"));

        Assert.Equal("custom_vocabulary is incompatible with diarization.", ex.Message);
        Assert.Equal("invalid_request", ex.InteractionErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    // PLAN-stt.md §3.2: a GET response's steps include a leading "user_input" echo step; Text()/
    // Words() must skip it and only aggregate "model_output" steps.
    [Fact]
    public void Interaction_Text_SkipsUserInputStepAndAggregatesModelOutputOnly()
    {
        var interaction = new Interaction
        {
            Steps =
            [
                new InteractionStep
                {
                    Type = "user_input",
                    Content = [new InteractionContent { Type = "audio", Uri = "files/abc", MimeType = "audio/wav" }],
                },
                new InteractionStep
                {
                    Type = "model_output",
                    Content = [new InteractionContent { Type = "text", Text = "hello world" }],
                },
            ],
        };

        Assert.Equal("hello world", interaction.Text());
        Assert.True(interaction.TryGetText(out var text));
        Assert.Equal("hello world", text);
        Assert.Equal("hello world", interaction.GetTextOrThrow());
    }

    [Fact]
    public void Interaction_Text_WhenNoModelOutputStep_ReturnsEmptyAndThrowsAppropriately()
    {
        var interaction = new Interaction { Steps = [] };

        Assert.Equal(string.Empty, interaction.Text());
        Assert.False(interaction.TryGetText(out var text));
        Assert.Null(text);
        Assert.Throws<GeminiContentException>(() => interaction.GetTextOrThrow());
    }

    // PLAN-stt.md §3.2: real diarized word annotations carry "spk:0"/"spk:1" speaker labels.
    [Fact]
    public void Interaction_Words_FlattensWordInfoAnnotationsWithSpeakerLabels()
    {
        var interaction = new Interaction
        {
            Steps =
            [
                new InteractionStep
                {
                    Type = "model_output",
                    Content =
                    [
                        new InteractionContent
                        {
                            Type = "text",
                            Text = "Hi Jane",
                            Annotations =
                            [
                                new InteractionAnnotation { Type = "word_info", Text = "Hi", Speaker = "spk:0" },
                                new InteractionAnnotation { Type = "word_info", Text = "Jane", Speaker = "spk:1" },
                            ],
                        },
                    ],
                },
            ],
        };

        var words = interaction.Words();
        Assert.Equal(2, words.Count);
        Assert.Equal("spk:0", words[0].Speaker);
        Assert.Equal("spk:1", words[1].Speaker);
    }

    // PLAN-stt.md §3.3: InteractionUsage.TotalOutputTokens reads 0 in every live call observed, even
    // with real output. GeminiClient.PostInteractionAsync must price output from
    // ModelInvocationTokenCounts, not the misleading top-level field, or this silently prices $0
    // output cost forever.
    [Fact]
    public async Task TranscribeAsync_UsageWithZeroTotalOutputTokens_StillPricesRealOutputFromModelInvocationTokenCounts()
    {
        const string ok = """
            {
              "id": "v1_abc",
              "status": "completed",
              "usage": {
                "total_tokens": 147,
                "total_input_tokens": 147,
                "total_output_tokens": 0,
                "model_invocation_token_counts": [
                  { "candidates_tokens_details": [{"modality": "text", "tokens": 14}] }
                ]
              },
              "steps": [{"type": "model_output", "content": [{"type": "text", "text": "hi"}]}]
            }
            """;
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var costGovernor = new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { });
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), costGovernor);
        var service = new TranscriptionService(client);

        await service.TranscribeAsync([1], "audio/wav");

        // gemini-3.5-transcribe's DefaultPricing: $2.00/1M input, $12.00/1M output.
        // If the mapper had read TotalOutputTokens (0) instead of ModelInvocationTokenCounts,
        // this would be exactly the input-only cost (147/1e6 * 2.00), not more.
        var inputOnlyCost = 147 / 1_000_000m * 2.00m;
        Assert.True(costGovernor.GetTodaySpend() > inputOnlyCost);
    }

    private static TranscriptionService CreateService(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        return new TranscriptionService(client);
    }
}
