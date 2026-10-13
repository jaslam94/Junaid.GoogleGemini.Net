using System.Net;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// The Voices API wrapper (<see cref="IVoiceService"/>) against the real API. See <c>PLAN-tts-3-8.md</c>.
///
/// Listing, getting and deleting use no model tokens, so most tests here cost nothing. Creating a voice runs a model
/// (about 660 tokens, under a cent). The two tests that need something only the maintainer has (their own voice, or a
/// long video) skip unless that is supplied.
/// </summary>
[Collection("Live")]
public class VoiceServiceLiveTests(GeminiFixture fixture)
{
    private const string TestVoicePrefix = "Gemini.Net live test";

    private IVoiceService Voices => fixture.Get<IVoiceService>();
    private IGeminiService Gemini => fixture.Get<IGeminiService>();
    private ITranscriptionService Transcription => fixture.Get<ITranscriptionService>();

    // ----- free: reading the voice library -----

    [RequiresGeminiKey]
    public async Task ListAsync_ReturnsPrebuiltVoices_WithTheDocumentedFields_AndANextPageToken()
    {
        var page = await Voices.ListAsync(new VoiceListOptions { Types = ["prebuilt"], PageSize = 5 });

        Assert.Equal(5, page.Voices!.Count);
        Assert.All(page.Voices, v =>
        {
            Assert.False(string.IsNullOrEmpty(v.Id));
            Assert.Equal("prebuilt", v.Type);
            Assert.False(string.IsNullOrEmpty(v.DisplayName));
            Assert.False(string.IsNullOrEmpty(v.LanguageCode));
            Assert.Null(v.ExpireTime); // only stored custom voices expire
        });
        Assert.False(string.IsNullOrEmpty(page.NextPageToken));
    }

    [RequiresGeminiKey]
    public async Task ListAsync_Filters_AreAcceptedByGoogle_AndNarrowTheResult()
    {
        // Google rejects an unknown parameter name with HTTP 400, so a pass here also proves the wrapper's
        // parameter names are real: language_code, gender and pitch (and, below, region_code, persona and context).
        var british = await Voices.ListAsync(new VoiceListOptions { LanguageCodes = ["en-GB"], PageSize = 50 });
        Assert.NotEmpty(british.Voices!);
        Assert.All(british.Voices!, v => Assert.Equal("en-GB", v.LanguageCode, ignoreCase: true));

        var lowFemale = await Voices.ListAsync(new VoiceListOptions { Genders = ["female"], Pitches = ["low"], PageSize = 50 });
        Assert.NotEmpty(lowFemale.Voices!);
        Assert.All(lowFemale.Voices!, v =>
        {
            Assert.Equal("female", v.Gender);
            Assert.Equal("low", v.Pitch);
        });

        // Values match exactly. The real persona is "Storyteller & Narrator", so "Narrator" alone finds nothing.
        var exact = await Voices.ListAsync(new VoiceListOptions { Personas = ["Storyteller & Narrator"], PageSize = 50 });
        var partial = await Voices.ListAsync(new VoiceListOptions { Personas = ["Narrator"], PageSize = 50 });
        Assert.NotEmpty(exact.Voices!);
        Assert.Empty(partial.Voices ?? []);

        var region = await Voices.ListAsync(new VoiceListOptions { RegionCodes = ["GB"], Contexts = ["Content & Media"], PageSize = 10 });
        Assert.All(region.Voices ?? [], v => Assert.Equal("GB", v.RegionCode));

        // Two values for one filter mean "either" (OR).
        var either = await Voices.ListAsync(new VoiceListOptions { LanguageCodes = ["en-GB", "fr-FR"], PageSize = 1000 });
        Assert.Contains(either.Voices!, v => v.LanguageCode == "en-GB");
        Assert.Contains(either.Voices!, v => v.LanguageCode == "fr-FR");
    }

    [RequiresGeminiKey]
    public async Task ListAllAsync_PagesThroughMoreThanOnePage_WithNoRepeats()
    {
        // Several dozen British voices, read 20 at a time, forces at least three pages.
        var ids = new List<string>();
        await foreach (var voice in Voices.ListAllAsync(new VoiceListOptions { LanguageCodes = ["en-GB"], PageSize = 20 }))
        {
            ids.Add(voice.Id!);
        }

        Assert.True(ids.Count > 40, $"Expected more than two pages of voices, got {ids.Count}.");
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [RequiresGeminiKey]
    public async Task GetAndDelete_OnAPrebuiltVoice_AreNotFound_AndChangeNothing()
    {
        // Live-verified on 2026-10-10: get and delete work only for your own stored voices.
        var get = await Assert.ThrowsAsync<GeminiApiException>(() => Voices.GetAsync("achernar"));
        var delete = await Assert.ThrowsAsync<GeminiApiException>(() => Voices.DeleteAsync("achernar"));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var stillThere = await Voices.ListAsync(new VoiceListOptions { Search = "achernar", PageSize = 5 });
        Assert.Contains(stillThere.Voices!, v => v.Id == "achernar");
    }

    [RequiresGeminiKey]
    public async Task GetAsync_OnAnUnknownCustomVoice_IsNotFound()
    {
        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Voices.GetAsync("voice_doesnotexist0000"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    // ----- billable: makes a real voice (about 660 tokens) -----

    [RequiresPaidGeminiKey]
    public async Task DesignedVoice_ThroughTheVoiceService_IsCreated_Listed_Used_AndDeleted()
    {
        await DeleteLeftoverTestVoicesAsync();

        var created = await Voices.CreateDesignedAsync(new DesignedVoiceRequest
        {
            Description = "A calm, friendly narrator in her thirties with a clear American accent.",
            DisplayName = $"{TestVoicePrefix} (safe to delete)",
            Gender = "female",
            LanguageCode = "en-US",
        });

        var id = created.Id!;
        try
        {
            Assert.StartsWith("voice_", id);
            Assert.Equal("prompted", created.Type);
            Assert.Equal("female", created.Gender);
            Assert.Equal("audio/wav", created.SampleAudio!.MimeType);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(created.SampleAudio.GetBytes(), 0, 4));
            // Stored voices live for a year. The real value was 365 days, so allow a wide margin.
            Assert.InRange((created.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalDays, 300, 400);

            var mine = await Voices.ListAsync(new VoiceListOptions { Types = ["prompted"] });
            Assert.Contains(mine.Voices!, v => v.Id == id);

            var fetched = await Voices.GetAsync(id);
            Assert.Equal(id, fetched.Id);
            Assert.NotNull(fetched.SampleAudio);

            // The id is the VoiceName, and it works with both 3.8 models.
            foreach (var model in new[] { GeminiConstants.Models.Gemini38FlashLiteTts, GeminiConstants.Models.Gemini38FlashTts })
            {
                var response = await Gemini.GenerateAudioAsync("Hello, this is my custom voice.",
                    new GeminiRequestOptions { Model = model, VoiceName = id });
                var wav = response.GetAudioOrThrow().ToWav();
                AudioGenerationLiveTests.AssertHasRealAudioSignal(wav);
                var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
                Assert.Contains("custom voice", transcript, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await Voices.DeleteAsync(id);
        }

        var gone = await Assert.ThrowsAsync<GeminiApiException>(() => Voices.GetAsync(id));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    // ----- opt-in: needs something only the maintainer has -----

    [RequiresOwnVoiceRecordings]
    public async Task ReplicatedVoice_FromTheMaintainersOwnRecordings_IsCreated_Used_AndDeleted()
    {
        // Voice replication copies a real person's voice and requires that person's consent. This test never makes a
        // recording. It reads two files that you recorded of yourself.
        await DeleteLeftoverTestVoicesAsync();
        var created = await Voices.CreateReplicatedAsync(new ReplicatedVoiceRequest
        {
            SourceAudio = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("GeminiTestVoiceSource")!),
            ConsentAudio = await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("GeminiTestVoiceConsent")!),
            DisplayName = $"{TestVoicePrefix} replicated (safe to delete)",
        });

        var id = created.Id!;
        try
        {
            Assert.StartsWith("voice_", id);
            Assert.Equal("replicated", created.Type);

            var response = await Gemini.GenerateAudioAsync("Hello, this is a replicated voice.",
                new GeminiRequestOptions { Model = GeminiConstants.Models.Gemini38FlashLiteTts, VoiceName = id });
            AudioGenerationLiveTests.AssertHasRealAudioSignal(response.GetAudioOrThrow().ToWav());

            var mine = await Voices.ListAsync(new VoiceListOptions { Types = ["replicated"] });
            Assert.Contains(mine.Voices!, v => v.Id == id);
        }
        finally
        {
            await Voices.DeleteAsync(id);
        }
    }

    [RequiresLongTestVideo]
    public async Task AgenticVideo_OnALongVideo_UsesFewerTokensThanStatic()
    {
        var path = Environment.GetEnvironmentVariable("GeminiTestLongVideo")!;
        var mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            _ => "video/mp4",
        };
        var files = fixture.Get<IFileService>();
        var uploaded = await files.UploadFileAsync(await File.ReadAllBytesAsync(path), mime, "gemininet-long-video-test");
        try
        {
            var active = await files.WaitUntilActiveAsync(uploaded.Name!, TimeSpan.FromSeconds(3), TimeSpan.FromMinutes(10));
            var options = new GeminiRequestOptions { ThinkingLevel = GeminiConstants.ThinkingLevels.Low };

            async Task<GenerateContentResponse> Ask(string? mode) => await Gemini.ChatAsync(
            [
                new Content
                {
                    Role = "user",
                    Parts =
                    [
                        new Part { FileData = new FileData { FileUri = active.Uri, MimeType = mime }, MediaProcessing = mode },
                        new Part { Text = "Summarize what happens in this video in three sentences." },
                    ]
                }
            ], options);

            var plain = await Ask(null);
            var agentic = await Ask(GeminiConstants.MediaProcessing.Agentic);

            var plainTotal = plain.Usage!.TotalTokenCount;
            var agenticTotal = agentic.Usage!.TotalTokenCount;
            var saved = 100.0 * (plainTotal - agenticTotal) / plainTotal;
            Console.WriteLine($"LONG VIDEO: static={plainTotal} tokens, agentic={agenticTotal} tokens, saved={saved:F0}% " +
                              $"(agentic tool-use tokens: {agentic.Usage.ToolUsePromptTokenCount}). Google claims up to 88%.");

            Assert.False(string.IsNullOrWhiteSpace(plain.Text()));
            Assert.False(string.IsNullOrWhiteSpace(agentic.Text()));
            Assert.True(agentic.Usage.ToolUsePromptTokenCount > 0);
            Assert.True(agenticTotal < plainTotal,
                $"Agentic used {agenticTotal} tokens and static used {plainTotal}. Expected a saving on a long video.");
        }
        finally
        {
            await files.DeleteFileAsync(uploaded.Name!);
        }
    }

    private async Task DeleteLeftoverTestVoicesAsync()
    {
        // Only voices that this test suite made, found by their name. Never anything else.
        var leftovers = new List<string>();
        await foreach (var voice in Voices.ListAllAsync(new VoiceListOptions { Types = ["prompted", "replicated"] }))
        {
            if (voice.DisplayName?.StartsWith(TestVoicePrefix, StringComparison.Ordinal) == true)
            {
                leftovers.Add(voice.Id!);
            }
        }

        foreach (var id in leftovers)
        {
            await Voices.DeleteAsync(id);
        }
    }
}
