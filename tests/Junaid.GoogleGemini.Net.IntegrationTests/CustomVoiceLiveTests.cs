using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Exceptions;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Custom voices, and the other Gemini 3.8 TTS options that need a real model to check. See
/// <c>PLAN-tts-3-8.md</c>.
///
/// A custom voice id (<c>voice_...</c>) comes from Google's Voices API. The library wraps that API in
/// <c>IVoiceService</c> (see <c>VoiceServiceLiveTests</c>). This test deliberately does NOT use the wrapper. It
/// calls the REST endpoint with a plain <see cref="HttpClient"/>, so it checks the wire format independently of the
/// wrapper's own code: create a "prompted" voice from a text description, use its id as
/// <see cref="GeminiRequestOptions.VoiceName"/>, then delete it. Stored voices
/// count against a limit of 200 per project and live for a year, so the test always deletes what it makes.
/// It also removes any leftover voice that starts with <see cref="TestVoicePrefix"/>, which a crashed run
/// could leave behind. It never touches a voice with another name.
/// </summary>
[Collection("Live")]
public class CustomVoiceLiveTests(GeminiFixture fixture)
{
    private const string TestVoicePrefix = "Gemini.Net live test";
    private static readonly Uri VoicesBase = new("https://generativelanguage.googleapis.com/v1beta/");

    private IGeminiService Gemini => fixture.Get<IGeminiService>();
    private ITranscriptionService Transcription => fixture.Get<ITranscriptionService>();

    // Paid tier: Google lists no free-tier status for voice design, so a free key may be refused.
    [RequiresPaidGeminiKey]
    public async Task DesignedVoice_IsCreated_UsedByBothModels_AndDeleted()
    {
        using var http = new HttpClient { BaseAddress = VoicesBase };
        http.DefaultRequestHeaders.Add("x-goog-api-key", Environment.GetEnvironmentVariable("GeminiApiKey"));
        await DeleteLeftoverTestVoicesAsync(http);

        // Do not set voice.model. Live-verified on 2026-10-10: gemini-3.8-flash-lite-tts answers HTTP 400
        // ("does not support Prompted Voice Design"), although Google's docs say both models do. The API
        // picks a default model, and the voice then works with either TTS model.
        var create = await http.PostAsJsonAsync("voices", new
        {
            store = true,
            voice = new
            {
                type = "prompted",
                display_name = $"{TestVoicePrefix} (safe to delete)",
                gender = "female",
                language_code = "en-US",
                prompted = new { input = "A calm, friendly narrator in her thirties with a clear American accent." },
            },
        });
        var body = await create.Content.ReadAsStringAsync();
        Assert.True(create.IsSuccessStatusCode, $"Voice design failed: {(int)create.StatusCode} {body}");
        var id = JsonNode.Parse(body)!["id"]!.GetValue<string>();
        Assert.StartsWith("voice_", id);

        try
        {
            foreach (var model in new[] { GeminiConstants.Models.Gemini38FlashLiteTts, GeminiConstants.Models.Gemini38FlashTts })
            {
                var response = await Gemini.GenerateAudioAsync(
                    "Hello, this is my custom voice.",
                    new GeminiRequestOptions { Model = model, VoiceName = id });

                var wav = response.GetAudioOrThrow().ToWav();
                AudioGenerationLiveTests.AssertHasRealAudioSignal(wav);
                var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
                Assert.Contains("custom voice", transcript, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            var delete = await http.DeleteAsync($"voices/{id}");
            Assert.True(delete.IsSuccessStatusCode, $"Could not delete the test voice {id}: {(int)delete.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"voices/{id}")).StatusCode);
    }

    [RequiresGeminiKey]
    public async Task UnknownCustomVoiceId_IsAnApiError_NotALibraryError()
    {
        // The library sends a voice_ id in voiceConfig.voice. The API looks it up and answers 404.
        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => Gemini.GenerateAudioAsync(
            "Hello.", new GeminiRequestOptions { VoiceName = "voice_doesnotexist0000" }));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [RequiresGeminiKey]
    public async Task ForcingTheOldMultiSpeakerShape_On38Flash_IsRejectedByTheApi()
    {
        // UseSpeechMetadata = false forces the 6.5.0 request shape. The 3.8 models reject it. This shows the
        // option really changes the request, and that the library's automatic choice is the one that works.
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini38FlashLiteTts,
            SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")],
            UseSpeechMetadata = false,
        };

        var ex = await Assert.ThrowsAsync<GeminiApiException>(
            () => Gemini.GenerateAudioAsync("Joe: Hi Jane! Jane: Hello Joe!", options));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("speech_metadata", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task InlineTag_IsAccepted_AndTheTagMarkupIsNotSpokenAloud()
    {
        // Google documents tags such as <laugh>. The library passes them through unchanged. We cannot assert
        // that a laugh was made, but the request must work and the literal markup must not be read out.
        var options = new GeminiRequestOptions { VoiceName = "Kore" };

        var response = await Gemini.GenerateAudioAsync("That is really funny <laugh> and I love it.", options);

        var wav = response.GetAudioOrThrow().ToWav();
        AudioGenerationLiveTests.AssertHasRealAudioSignal(wav);
        var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
        Assert.Contains("funny", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("love it", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", transcript);
        Assert.DoesNotContain("angle bracket", transcript, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DeleteLeftoverTestVoicesAsync(HttpClient http)
    {
        var list = await http.GetAsync("voices?type=prompted&pageSize=100");
        if (!list.IsSuccessStatusCode)
        {
            return;
        }

        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("voices", out var voices))
        {
            return;
        }

        foreach (var voice in voices.EnumerateArray())
        {
            var name = voice.TryGetProperty("display_name", out var n) ? n.GetString() : null;
            if (name is not null && name.StartsWith(TestVoicePrefix, StringComparison.Ordinal))
            {
                await http.DeleteAsync($"voices/{voice.GetProperty("id").GetString()}");
            }
        }
    }
}
