using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests against the real Gemini TTS models. See <c>PLAN-tts.md</c> for the full research this
/// feature was built from, including the request/response shape live-verified before any code was
/// written.
///
/// Uses the plain <see cref="RequiresGeminiKeyAttribute"/>, not the paid-tier one, for
/// <c>gemini-2.5-flash-preview-tts</c>: Google's pricing page lists it as free-tier available, but
/// this session only had a billing-enabled key to test with, so that claim is unconfirmed either way
/// (a paid key passing this test proves nothing about whether a free-tier key also would). Do not
/// upgrade this to <see cref="RequiresPaidGeminiKeyAttribute"/> without a free-tier key actually
/// failing here first, per <c>PLAN-tts.md</c> §6.
/// </summary>
[Collection("Live")]
public class AudioGenerationLiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_SingleSpeaker_ReturnsPlayableWav()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini25FlashTts,
            VoiceName = "Kore",
        };

        var response = await Gemini.GenerateAudioAsync("Say cheerfully: Have a wonderful day!", options);

        var audio = response.GetAudioOrThrow();
        Assert.StartsWith("audio/", audio.MimeType);
        Assert.NotEmpty(audio.Data);

        AssertLooksLikeAWavFile(audio.ToWav());
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_MultiSpeaker_ReturnsPlayableWav()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini25FlashTts,
            SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")],
        };

        var response = await Gemini.GenerateAudioAsync(
            "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!",
            options);

        var audio = response.GetAudioOrThrow();
        AssertLooksLikeAWavFile(audio.ToWav());
    }

    [RequiresGeminiKey]
    public async Task StreamAudioAsync_YieldsAudioAndFinalUsageMetadata()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini25FlashTts,
            VoiceName = "Kore",
        };

        var chunks = new List<Models.GoogleApi.GenerateContentResponse>();
        await foreach (var chunk in Gemini.StreamAudioAsync("Say cheerfully: Have a wonderful day!", options))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Audio() is not null);

        // The API's own docs only promise usage on the final chunk; a short clip may arrive as a
        // single chunk, so this just confirms usage showed up somewhere with an AUDIO modality tag.
        var usageChunk = chunks.LastOrDefault(c => c.Usage is not null);
        Assert.NotNull(usageChunk);
        Assert.Contains(
            usageChunk!.Usage!.CandidatesTokensDetails ?? [],
            d => string.Equals(d.Modality, "AUDIO", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Confirms the bytes are a well-formed RIFF/WAVE file, not just any non-empty array.</summary>
    private static void AssertLooksLikeAWavFile(byte[] wav)
    {
        Assert.True(wav.Length > 44, "Expected more than just the WAV header.");
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
    }
}
