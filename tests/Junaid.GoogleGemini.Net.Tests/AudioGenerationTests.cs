using System.Net;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Options;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

public class AudioGenerationTests
{
    [Fact]
    public void RequestFactory_NoSpeechOptions_OmitsResponseModalitiesAndSpeechConfig()
    {
        var request = RequestFactory.CreateTextRequest("hello", options: null);
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.DoesNotContain("responseModalities", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("speechConfig", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestFactory_WithVoiceName_SerializesSingleSpeakerSpeechConfig()
    {
        var request = RequestFactory.CreateTextRequest("hello",
            new GeminiRequestOptions { VoiceName = "Kore" });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains(
            "\"speechConfig\":{\"voiceConfig\":{\"prebuiltVoiceConfig\":{\"voiceName\":\"Kore\"}}}",
            json);
        Assert.DoesNotContain("multiSpeakerVoiceConfig", json);
    }

    [Fact]
    public void RequestFactory_WithSpeakerVoices_SerializesMultiSpeakerSpeechConfig()
    {
        var request = RequestFactory.CreateTextRequest("Joe: Hi! Jane: Hello!",
            new GeminiRequestOptions
            {
                SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")]
            });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains(
            "\"multiSpeakerVoiceConfig\":{\"speakerVoiceConfigs\":[" +
            "{\"speaker\":\"Joe\",\"voiceConfig\":{\"prebuiltVoiceConfig\":{\"voiceName\":\"Kore\"}}}," +
            "{\"speaker\":\"Jane\",\"voiceConfig\":{\"prebuiltVoiceConfig\":{\"voiceName\":\"Puck\"}}}" +
            "]}",
            json);
    }

    [Fact]
    public void RequestFactory_BothVoiceNameAndSpeakerVoicesSet_SpeakerVoicesWins()
    {
        // A script naming multiple speakers cannot sensibly also use one global voice; the plural,
        // more specific option must take priority.
        var request = RequestFactory.CreateTextRequest("Joe: Hi! Jane: Hello!",
            new GeminiRequestOptions
            {
                VoiceName = "Zephyr",
                SpeakerVoices = [("Joe", "Kore"), ("Jane", "Puck")]
            });
        var json = JsonSerializer.Serialize(request, GeminiJson.Default);

        Assert.Contains("multiSpeakerVoiceConfig", json);
        Assert.DoesNotContain("Zephyr", json);
        Assert.DoesNotContain("\"voiceConfig\":{\"prebuiltVoiceConfig\":{\"voiceName\":\"Zephyr\"", json);
    }

    [Fact]
    public async Task GenerateAudioAsync_WhenModelAndModalitiesUnset_AppliesDefaults()
    {
        const string ok = """{"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"audio/L16;codec=pcm;rate=24000","data":"AAAA"}}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var response = await service.GenerateAudioAsync("Say cheerfully: Have a wonderful day!");

        Assert.Contains(GeminiConstants.Models.RecommendedTts, handler.Requests[0].RequestUri!.ToString());

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"responseModalities\":[\"AUDIO\"]", body);

        var audio = response.GetAudioOrThrow();
        Assert.Equal("audio/L16;codec=pcm;rate=24000", audio.MimeType);
    }

    [Fact]
    public async Task GenerateAudioAsync_CallerSetModelAndVoice_LeavesModelAloneAndSendsVoice()
    {
        const string ok = """{"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"audio/L16;codec=pcm;rate=24000","data":"AAAA"}}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        await service.GenerateAudioAsync("hello", new GeminiRequestOptions
        {
            Model = GeminiConstants.Models.Gemini31FlashTts,
            VoiceName = "Puck"
        });

        Assert.Contains(GeminiConstants.Models.Gemini31FlashTts, handler.Requests[0].RequestUri!.ToString());
        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"voiceName\":\"Puck\"", body);
    }

    [Fact]
    public async Task GenerateAudioAsync_WhenApiReturnsNoCandidates_ThrowsWithAudioGenerationLabel()
    {
        const string emptyResponse = """{"candidates":[]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, emptyResponse);
        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateAudioAsync("hello"));

        Assert.Contains("audio generation", ex.Message);
        Assert.DoesNotContain("text generation", ex.Message);
    }

    [Fact]
    public async Task Audio_TryGetAudio_GetAudioOrThrow_AgreeOnAPresentAudioPart()
    {
        const string ok = """{"candidates":[{"content":{"role":"model","parts":[{"inlineData":{"mimeType":"audio/L16;codec=pcm;rate=24000","data":"AAECAw=="}}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, ok);
        var service = CreateService(handler);

        var response = await service.GenerateAudioAsync("hello");

        var audio = response.Audio();
        Assert.NotNull(audio);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, audio!.Data);

        Assert.True(response.TryGetAudio(out var tryAudio));
        Assert.Equal(audio.Data, tryAudio!.Data);

        Assert.Equal(audio.Data, response.GetAudioOrThrow().Data);
    }

    [Fact]
    public async Task Audio_WhenNoAudioPart_ReturnsNullAndThrowsAppropriately()
    {
        const string textOnly = """{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}]}""";
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, textOnly);
        var service = CreateService(handler);

        var response = await service.GenerateAsync("hello");

        Assert.Null(response.Audio());
        Assert.False(response.TryGetAudio(out var tryAudio));
        Assert.Null(tryAudio);
        Assert.Throws<Junaid.GoogleGemini.Net.Exceptions.GeminiContentException>(() => response.GetAudioOrThrow());
    }

    [Fact]
    public void GeneratedAudio_ToWav_ProducesACorrectHeaderForKnownInput()
    {
        var pcm = new byte[] { 1, 2, 3, 4 };
        var audio = new Junaid.GoogleGemini.Net.Models.GoogleApi.GeneratedAudio(
            "audio/L16;codec=pcm;rate=24000", pcm);

        var wav = audio.ToWav();

        Assert.Equal(44 + pcm.Length, wav.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(36 + pcm.Length, BitConverter.ToInt32(wav, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", System.Text.Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(16, BitConverter.ToInt32(wav, 16)); // Subchunk1Size
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // AudioFormat: PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22)); // NumChannels: mono
        Assert.Equal(24000, BitConverter.ToInt32(wav, 24)); // SampleRate, parsed from mimeType
        Assert.Equal(24000 * 1 * 2, BitConverter.ToInt32(wav, 28)); // ByteRate
        Assert.Equal(2, BitConverter.ToInt16(wav, 32)); // BlockAlign
        Assert.Equal(16, BitConverter.ToInt16(wav, 34)); // BitsPerSample
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(pcm.Length, BitConverter.ToInt32(wav, 40));
        Assert.Equal(pcm, wav[44..]);
    }

    [Fact]
    public void GeneratedAudio_ToWav_ThrowsOnAnUnrecognizedMimeType()
    {
        var audio = new Junaid.GoogleGemini.Net.Models.GoogleApi.GeneratedAudio(
            "audio/mpeg", [1, 2, 3]);

        Assert.Throws<FormatException>(() => audio.ToWav());
    }

    private static GeminiService CreateService(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(httpClient, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345" });
        return new GeminiService(client, NullLogger<GeminiService>.Instance, options, new SafetyService(), GeminiCostGovernor.CreateDisabled());
    }
}
