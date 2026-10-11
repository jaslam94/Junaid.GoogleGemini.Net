using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Xunit;

namespace Junaid.GoogleGemini.Net.IntegrationTests;

/// <summary>
/// Live tests against the real Gemini 3.8 TTS models. See <c>PLAN-tts-3-8.md</c> for the research
/// these tests came from, and <c>PLAN-tts.md</c> for the original feature.
///
/// Google shuts down the three older TTS models (2.5 Flash, 2.5 Pro and 3.1 Flash) on 2026-11-17, so
/// these tests use only the 3.8 models. The older request shape is covered by unit tests, and was
/// checked live against the real API on 2026-10-10 before 6.8.0 shipped.
///
/// Several tests send the audio back through <see cref="ITranscriptionService"/>. Checking that a WAV file
/// is well formed does not show what the model said. A transcript does. That is how these tests catch
/// a style direction that the model read aloud, or a preamble that leaked into the speech.
///
/// Both 3.8 models list a free tier on Google's pricing page, so these use the plain
/// <see cref="RequiresGeminiKeyAttribute"/>.
/// </summary>
[Collection("Live")]
public class AudioGenerationLiveTests(GeminiFixture fixture)
{
    private IGeminiService Gemini => fixture.Get<IGeminiService>();
    private ITranscriptionService Transcription => fixture.Get<ITranscriptionService>();

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_DefaultModel_ReturnsAPlayableWav()
    {
        // No model, no format: the defaults a first-time caller gets. The 3.8 models answer a unary
        // call with a complete WAV file, and ToWav() must hand it back untouched.
        var response = await Gemini.GenerateAudioAsync("Have a wonderful day.", new GeminiRequestOptions { VoiceName = "Kore" });

        var audio = response.GetAudioOrThrow();
        Assert.Equal("audio/wav", audio.MimeType);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(audio.Data, 0, 4));
        Assert.Same(audio.Data, audio.ToWav());
        AssertHasRealAudioSignal(audio.ToWav());
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_FlashTts_ReturnsAPlayableWav()
    {
        var options = new GeminiRequestOptions { Model = GeminiConstants.Models.Gemini38FlashTts, VoiceName = "Puck" };

        var response = await Gemini.GenerateAudioAsync("Have a wonderful day.", options);

        AssertHasRealAudioSignal(response.GetAudioOrThrow().ToWav());
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_AudioFormatPcm16_ReturnsRawPcmThatToWavWraps()
    {
        var options = new GeminiRequestOptions
        {
            VoiceName = "Kore",
            AudioFormat = GeminiConstants.AudioFormats.Pcm16,
        };

        var response = await Gemini.GenerateAudioAsync("Have a wonderful day.", options);

        var audio = response.GetAudioOrThrow();
        Assert.Equal("audio/l16; rate=24000; channels=1", audio.MimeType);
        Assert.NotEqual("RIFF", System.Text.Encoding.ASCII.GetString(audio.Data, 0, 4));
        AssertHasRealAudioSignal(audio.ToWav());
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_SpeechStyle_IsNotSpokenAloud()
    {
        // Before 6.8.0 the way to give a direction was to write it in the prompt: "Say cheerfully: ...".
        // The 3.8 models speak the whole prompt, so that read the direction out loud. SpeechStyle sends
        // it as speech metadata instead.
        var options = new GeminiRequestOptions { VoiceName = "Kore", SpeechStyle = "cheerful and upbeat" };

        var response = await Gemini.GenerateAudioAsync("Have a wonderful day.", options);

        var wav = response.GetAudioOrThrow().ToWav();
        AssertHasRealAudioSignal(wav);
        var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
        Assert.Contains("wonderful day", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cheerful", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upbeat", transcript, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_MultiSpeaker_SplitsTheScriptAndDropsThePreamble()
    {
        // This is the exact prompt from PLAN-tts.md and the docs. Before 6.8.0 it worked on the old
        // models. On a 3.8 model the same request, sent as one text part, returned HTTP 400.
        var options = new GeminiRequestOptions { SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")] };

        var response = await Gemini.GenerateAudioAsync(
            "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!",
            options);

        var wav = response.GetAudioOrThrow().ToWav();
        AssertHasRealAudioSignal(wav);

        var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
        Assert.Contains("Hi Jane", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello Joe", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversation", transcript, StringComparison.OrdinalIgnoreCase); // the preamble was dropped
    }

    [RequiresGeminiKey]
    public async Task StreamAudioAsync_MultiSpeaker_SplitsTheScriptToo()
    {
        // Streaming builds its request through a different method than a unary call. This proves the
        // script split reaches both.
        var options = new GeminiRequestOptions { SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")] };

        var pcm = new List<byte>();
        string? mimeType = null;
        await foreach (var chunk in Gemini.StreamAudioAsync(
            "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!", options))
        {
            if (chunk.Audio() is { } clip)
            {
                mimeType ??= clip.MimeType;
                pcm.AddRange(clip.Data);
            }
        }

        Assert.NotNull(mimeType);
        var wav = new Models.GoogleApi.GeneratedAudio(mimeType!, pcm.ToArray()).ToWav();
        AssertHasRealAudioSignal(wav);

        var transcript = (await Transcription.TranscribeAsync(wav, "audio/wav")).GetTextOrThrow();
        Assert.Contains("Hi Jane", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello Joe", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversation", transcript, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresGeminiKey]
    public async Task GenerateAudioAsync_LanguageCode_IsAccepted()
    {
        var options = new GeminiRequestOptions { VoiceName = "Kore", SpeechLanguageCode = "en-US" };

        var response = await Gemini.GenerateAudioAsync("Have a wonderful day.", options);

        AssertHasRealAudioSignal(response.GetAudioOrThrow().ToWav());
    }

    [RequiresGeminiKey]
    public async Task StreamAudioAsync_YieldsPcmAudioAndFinalUsageMetadata()
    {
        var options = new GeminiRequestOptions { VoiceName = "Kore" };

        var chunks = new List<Models.GoogleApi.GenerateContentResponse>();
        await foreach (var chunk in Gemini.StreamAudioAsync("Have a wonderful day, and thank you for listening today.", options))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        var audioChunks = chunks.Select(c => c.Audio()).Where(a => a is not null).Select(a => a!).ToList();
        Assert.NotEmpty(audioChunks);
        // A stream defaults to raw PCM, so every chunk can go straight through ToWav().
        Assert.All(audioChunks, a => Assert.StartsWith("audio/l16", a.MimeType));
        var allPcm = audioChunks.SelectMany(a => a.Data).ToArray();
        AssertHasRealAudioSignal(new Models.GoogleApi.GeneratedAudio(audioChunks[0].MimeType, allPcm).ToWav());

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
    internal static void AssertHasRealAudioSignal(byte[] wav)
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
