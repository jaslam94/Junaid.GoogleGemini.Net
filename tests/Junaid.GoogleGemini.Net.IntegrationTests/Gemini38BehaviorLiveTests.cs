using System.Net;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Pins the behaviors this library documents from live observation on 2026-10-10 (see
/// <c>PLAN-tts-3-8.md</c> section 2). Google changes models without notice. If one of these fails in the
/// weekly run, the docs and the release notes are now wrong, and the failing test says which claim.
///
/// Every test here costs a fraction of a cent. The video test is the only one that needs a local file.
/// </summary>
[Collection("Live")]
public class Gemini38BehaviorLiveTests(GeminiFixture fixture)
{
    private const string Prompt = "Reply with the single word OK.";

    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    // ----- thinking -----

    [RequiresGeminiKey]
    public async Task ThinkingLevelMinimal_On38Flash_IsRejectedWithBadRequest()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini38Flash,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Minimal,
        };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Gemini.GenerateAsync(Prompt, options));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("MINIMAL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task ThinkingBudgetZero_IsAccepted_AndUsuallyTurnsThinkingOff_On36And38Flash()
    {
        // Google's docs tell 3.6 and later users to use ThinkingLevel. Budget 0 is undocumented there. It is
        // accepted, and it usually gives no thought tokens, but it is NOT a guarantee: the first version of
        // this test asserted "always zero" and failed on a live run, because 1 call in 8 on gemini-3.8-flash
        // still thought for 67 tokens. So this checks what is true: every call is accepted and answers, and
        // at least one of five calls on each model has no thought tokens. A model that ignored the field would
        // fail that, and the chance that 5 calls in a row all think is under 1 in 30,000 at the rate we saw.
        foreach (var model in new[] { GeminiConstants.Models.Gemini36Flash, GeminiConstants.Models.Gemini38Flash })
        {
            var thoughts = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                var response = await Gemini.GenerateAsync(Prompt, new GeminiRequestOptions { Model = model, ThinkingBudget = 0 });

                Assert.False(string.IsNullOrWhiteSpace(response.Text()), model);
                thoughts.Add(response.Usage!.ThoughtsTokenCount);
            }

            Assert.True(thoughts.Contains(0), $"{model}: every call thought despite a budget of 0 ({string.Join(", ", thoughts)}).");
        }
    }

    // ----- request rules -----

    [RequiresGeminiKey]
    public async Task HistoryEndingInAModelTurn_IsRejectedWithBadRequest()
    {
        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = Prompt }] },
            new() { Role = "model", Parts = [new Part { Text = "The answer is" }] },
        };
        var options = new GeminiRequestOptions { Model = GeminiConstants.Models.Gemini38Flash };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Gemini.ChatAsync(contents, options));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("model turn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task SamplingParameters_AreAcceptedAndIgnored_On38Flash()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini38Flash,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Temperature = 0.1f,
            TopK = 5,
        };

        var response = await Gemini.GenerateAsync(Prompt, options);

        Assert.False(string.IsNullOrWhiteSpace(response.Text()));
    }

    // ----- deprecated models are routed -----

    [RequiresGeminiKey]
    public async Task DeprecatedModels_AreRoutedToTheirReplacements()
    {
        // Strings, not the constants: the constants are [Obsolete]. The cost governor prices these two names
        // as the models in the right-hand column, so a change here means those prices are wrong.
        (string Asked, string Answers)[] routes =
        [
            ("gemini-3.5-flash", "gemini-3.6-flash"),
            ("gemini-3.7-flash", "gemini-3.8-flash"),
        ];

        foreach (var (asked, answers) in routes)
        {
            var response = await Gemini.GenerateAsync(Prompt, new GeminiRequestOptions
            {
                Model = asked,
                ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            });

            Assert.Equal(answers, response.ModelVersion);
        }
    }

    // ----- function calls carry ids -----

    [RequiresGeminiKey]
    public async Task ParallelFunctionCalls_On38Flash_CarryIds_AndTheReplyCanEchoThem()
    {
        var getWeather = new FunctionDeclaration
        {
            Name = "get_weather",
            Description = "Get the current temperature for a city, in Celsius.",
            Parameters = JsonNode.Parse(
                """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""")
        };
        const string prompt = "What is the temperature in Paris and in London? Call get_weather once for each city.";
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini38Flash,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
            ToolConfig = new ToolConfig { FunctionCallingConfig = new FunctionCallingConfig { Mode = "ANY" } },
        };

        var first = await Gemini.GenerateAsync(prompt, options);
        var modelTurn = first.Candidates![0].Content!;
        var calls = modelTurn.Parts.Where(p => p.FunctionCall is not null).Select(p => p.FunctionCall!).ToList();

        Assert.True(calls.Count >= 2, $"Expected one call per city, got {calls.Count}.");
        Assert.All(calls, c => Assert.False(string.IsNullOrEmpty(c.Id), "Gemini 3.8 should return a call id."));
        Assert.Equal(calls.Count, calls.Select(c => c.Id).Distinct().Count());

        // Answer each call by id, in reverse order. Only the ids tell the model which answer is which.
        var replies = calls.AsEnumerable().Reverse().Select(c => new Part
        {
            FunctionResponse = new FunctionResponsePart
            {
                Id = c.Id,
                Name = c.Name,
                Response = JsonNode.Parse($$"""{"temperatureC":{{(c.Args?["city"]?.ToString() == "Paris" ? 18 : 11)}}}"""),
            }
        }).ToList();
        var contents = new List<Content>
        {
            new() { Role = "user", Parts = [new Part { Text = prompt }] },
            modelTurn,
            new() { Role = "user", Parts = replies },
        };

        var second = await Gemini.ChatAsync(contents, new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini38Flash,
            ThinkingLevel = GeminiConstants.ThinkingLevels.Low,
            Functions = [getWeather],
        });

        Assert.Equal("STOP", second.FinishReason);
        Assert.Contains("18", second.Text());
        Assert.Contains("11", second.Text());
    }

    // ----- agentic video (needs a local clip, see RequiresTestVideoAttribute) -----

    [RequiresTestVideo]
    public async Task AgenticVideo_ReportsToolUseTokens_AndTheCostGovernorCountsThem()
    {
        var bytes = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("GeminiTestVideo")!);
        var options = new GeminiRequestOptions { ThinkingLevel = GeminiConstants.ThinkingLevels.Low };

        var plain = await Gemini.ChatAsync(VideoRequest(InlineVideo(bytes, null)), options);
        var agentic = await Gemini.ChatAsync(VideoRequest(InlineVideo(bytes, GeminiConstants.MediaProcessing.Agentic)), options);

        Assert.False(string.IsNullOrWhiteSpace(plain.Text()));
        Assert.False(string.IsNullOrWhiteSpace(agentic.Text()), "The answer follows the toolCall and toolResponse parts.");
        Assert.Equal(0, plain.Usage!.ToolUsePromptTokenCount);
        Assert.True(plain.Usage.PromptTokenCount > 100, "A plain video should count as prompt tokens.");
        Assert.True(agentic.Usage!.ToolUsePromptTokenCount > 100, "Agentic video should report its frames as tool-use tokens.");
        Assert.True(agentic.Usage.PromptTokenCount < plain.Usage.PromptTokenCount, "The video leaves the prompt count.");

        // Before 6.8.0 the governor ignored tool-use tokens and priced the whole video as free.
        var governor = new GeminiCostGovernor(new BudgetOptions { Enabled = true }, (_, _) => { });
        var honest = governor.RecordSpend(GeminiConstants.Models.Gemini38Flash, agentic.Usage);
        var ignoringToolUse = governor.RecordSpend(GeminiConstants.Models.Gemini38Flash, new UsageMetadata
        {
            PromptTokenCount = agentic.Usage.PromptTokenCount,
            CandidatesTokenCount = agentic.Usage.CandidatesTokenCount,
            ThoughtsTokenCount = agentic.Usage.ThoughtsTokenCount,
        });
        Assert.True(honest > ignoringToolUse * 2, $"honest={honest}, ignoringToolUse={ignoringToolUse}");
    }

    [RequiresTestVideo]
    public async Task AgenticVideo_OnAnUploadedFile_WorksWithAndWithoutAMimeType()
    {
        // The first version of this test expected HTTP 400 without a mime type, because a YouTube URL gave
        // that. A file uploaded through the Files API does not need one: the API already knows its type.
        var files = fixture.Get<IFileService>();
        var bytes = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("GeminiTestVideo")!);
        var uploaded = await files.UploadFileAsync(bytes, "video/mp4", "gemininet-agentic-test.mp4");
        try
        {
            var active = await files.WaitUntilActiveAsync(uploaded.Name!, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));
            var options = new GeminiRequestOptions { ThinkingLevel = GeminiConstants.ThinkingLevels.Low };

            Part Video(string? mime) => new()
            {
                FileData = new FileData { FileUri = active.Uri, MimeType = mime },
                MediaProcessing = GeminiConstants.MediaProcessing.Agentic,
            };

            foreach (var mime in new[] { "video/mp4", null })
            {
                var response = await Gemini.ChatAsync(VideoRequest(Video(mime)), options);

                Assert.True(response.Usage!.ToolUsePromptTokenCount > 100, $"mime={mime ?? "(none)"}");
                Assert.False(string.IsNullOrWhiteSpace(response.Text()), $"mime={mime ?? "(none)"}");
            }
        }
        finally
        {
            await files.DeleteFileAsync(uploaded.Name!);
        }
    }

    private static Part InlineVideo(byte[] bytes, string? mediaProcessing) => new()
    {
        InlineData = new InlineData { MimeType = "video/mp4", Data = Convert.ToBase64String(bytes) },
        MediaProcessing = mediaProcessing,
    };

    private static List<Content> VideoRequest(Part video) =>
    [
        new()
        {
            Role = "user",
            Parts = [video, new Part { Text = "Describe what happens in this video in two sentences." }]
        }
    ];
}
