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

        AssertHasRealAudioSignal(audio.ToWav());
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
        AssertHasRealAudioSignal(audio.ToWav());
    }

    // gemini-2.5-flash-preview-tts is the only model exercised above. These two cover the other
    // constants this library ships (Gemini25ProTts, Gemini31FlashTts): neither had ever been called
    // through the actual library code before, only assumed to share the same shape per Google's docs
    // (see PLAN-tts.md §2). gemini-2.5-pro-preview-tts is documented as paid-tier only; this uses the
    // plain RequiresGeminiKey rather than RequiresPaidGeminiKey for the same reason as the class-level
    // remarks above, this session only ever had a paid key to test with, so a pass here does not
    // independently confirm the paid-tier restriction either way.
    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_Gemini25ProTts_ReturnsPlayableWav()
    {
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini25ProTts,
            VoiceName = "Kore",
        };

        var response = await Gemini.GenerateAudioAsync("Say cheerfully: Have a wonderful day!", options);

        var audio = response.GetAudioOrThrow();
        Assert.Equal("audio/L16;codec=pcm;rate=24000", audio.MimeType);
        AssertHasRealAudioSignal(audio.ToWav());
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_Gemini31FlashTts_ReturnsPlayableWav()
    {
        // Confirmed live: this model returns a genuinely different mimeType shape than the 2.5-era
        // models above (lowercase "l16", spaced parameters, "channels=" instead of "codec=pcm"). A
        // first version of this test asserted the 2.5-era string here and failed against this
        // model's real response; GeneratedAudio.ToWav() was fixed to parse both shapes (see
        // PLAN-tts.md §2), and this assertion now checks the format actually confirmed, not an
        // assumption carried over from the other model.
        var options = new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini31FlashTts,
            VoiceName = "Kore",
        };

        var response = await Gemini.GenerateAudioAsync("Say cheerfully: Have a wonderful day!", options);

        var audio = response.GetAudioOrThrow();
        Assert.Equal("audio/l16; rate=24000; channels=1", audio.MimeType);
        AssertHasRealAudioSignal(audio.ToWav());
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

    /// <summary>
    /// Confirms the bytes are a well-formed RIFF/WAVE file AND that the PCM payload is real audio,
    /// not silence or garbage behind a valid-looking header. A header-only check would pass for 1,000
    /// zero bytes; this reads the actual 16-bit samples and requires real variance across them, the
    /// way genuine speech has and pure silence or a corrupt/constant buffer does not.
    /// </summary>
    private static void AssertHasRealAudioSignal(byte[] wav)
    {
        Assert.True(wav.Length > 44, "Expected more than just the WAV header.");
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // AudioFormat: PCM
        Assert.Equal(16, BitConverter.ToInt16(wav, 34)); // BitsPerSample

        var sampleCount = (wav.Length - 44) / 2;
        Assert.True(sampleCount > 100, $"Expected a real clip's worth of samples, got {sampleCount}.");

        short min = short.MaxValue, max = short.MinValue;
        long sumOfSquares = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(wav, 44 + i * 2);
            if (sample < min) min = sample;
            if (sample > max) max = sample;
            sumOfSquares += (long)sample * sample;
        }

        // Silence, a corrupt all-zero buffer, or a stuck constant value would all have a range of 0
        // and an RMS of 0. Genuine speech does not. Neither threshold is tuned to anything precise;
        // both exist purely to fail loudly on "this isn't real audio," not to validate audio quality.
        Assert.True(max - min > 50, $"Sample range too small to be real audio (min={min}, max={max}).");
        var rms = Math.Sqrt(sumOfSquares / (double)sampleCount);
        Assert.True(rms > 10, $"RMS too low to be real audio (rms={rms:F2}).");
    }
}
