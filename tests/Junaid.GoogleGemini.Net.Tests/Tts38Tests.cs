using System.Text.Json;
using System.Text.Json.Nodes;
using Junaid.GoogleGemini.Net.Infrastructure.Factories;
using Junaid.GoogleGemini.Net.Infrastructure.Serialization;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Models.Requests;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests;

/// <summary>
/// Request shapes for the Gemini 3.8 TTS models and the 6.8.0 model refresh. Every expected shape here was
/// confirmed against the real API on 2026-10-10. See PLAN-tts-3-8.md section 2.
/// </summary>
public class Tts38Tests
{
    private const string Script = "Joe: Hi Jane! Jane: Hello Joe!";

    private static readonly IReadOnlyList<(string Speaker, string VoiceName)> JoeAndJane =
        [("Joe", "Kore"), ("Jane", "Puck")];

    private static List<Part> PartsOf(GenerateContentRequest request) => request.Contents![0].Parts!;

    private static string Json(GenerateContentRequest request) => JsonSerializer.Serialize(request, GeminiJson.Default);

    // The splitter is internal, so these tests reach it the way a caller does: through the request factory.
    private static List<(string Speaker, string Text)> Turns(string script, string[] speakers) =>
        PartsOf(RequestFactory.CreateTextRequest(script, new GeminiRequestOptions
        {
            Model = "gemini-3.8-flash-tts",
            SpeakerVoices = speakers.Select(s => (s, "Kore")).ToList()
        })).Select(p => (p.SpeechMetadata!.Speaker!, p.Text!)).ToList();

    // ----- multi-speaker: the shape depends on the model -----

    [Theory]
    [InlineData(null)]
    [InlineData("gemini-3.8-flash-tts")]
    [InlineData("gemini-3.8-flash-lite-tts")]
    [InlineData("models/gemini-3.8-flash-tts")]
    [InlineData("gemini-4-flash-tts")] // a model we do not know yet gets the newer shape
    public void SpeakerVoices_OnANewerModel_SendsOnePartPerTurnWithSpeakerMetadata(string? model)
    {
        var request = RequestFactory.CreateTextRequest(Script,
            new GeminiRequestOptions { Model = model, SpeakerVoices = JoeAndJane });

        var parts = PartsOf(request);
        Assert.Equal(2, parts.Count);
        Assert.Equal("Hi Jane!", parts[0].Text);
        Assert.Equal("Joe", parts[0].SpeechMetadata!.Speaker);
        Assert.Equal("Hello Joe!", parts[1].Text);
        Assert.Equal("Jane", parts[1].SpeechMetadata!.Speaker);

        var json = Json(request);
        Assert.Contains("\"speechMetadata\":{\"speaker\":\"Joe\"}", json);
        Assert.DoesNotContain("\"style\"", json);
        Assert.DoesNotContain("Joe:", json); // the labels leave the text, or the model would read them aloud
    }

    [Theory]
    [InlineData("gemini-2.5-flash-preview-tts")]
    [InlineData("gemini-2.5-pro-preview-tts")]
    [InlineData("gemini-3.1-flash-tts-preview")]
    [InlineData("models/gemini-3.1-flash-tts-preview")]
    [InlineData("GEMINI-2.5-FLASH-PREVIEW-TTS")]
    public void SpeakerVoices_OnAnOlderModel_SendsTheWholePromptUnchanged(string model)
    {
        // These models reject speechMetadata with HTTP 400 ("Speech metadata is not supported for this model").
        const string prompt = "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!";
        var request = RequestFactory.CreateTextRequest(prompt,
            new GeminiRequestOptions { Model = model, SpeakerVoices = JoeAndJane });

        var parts = PartsOf(request);
        Assert.Single(parts);
        Assert.Equal(prompt, parts[0].Text);
        Assert.DoesNotContain("speechMetadata", Json(request));
    }

    [Fact]
    public void UseSpeechMetadata_Override_BeatsTheModelName()
    {
        var forcedOn = RequestFactory.CreateTextRequest(Script, new GeminiRequestOptions
        {
            Model = "gemini-2.5-flash-preview-tts",
            SpeakerVoices = JoeAndJane,
            UseSpeechMetadata = true
        });
        var forcedOff = RequestFactory.CreateTextRequest(Script, new GeminiRequestOptions
        {
            Model = "gemini-3.8-flash-tts",
            SpeakerVoices = JoeAndJane,
            UseSpeechMetadata = false
        });

        Assert.Equal(2, PartsOf(forcedOn).Count);
        Assert.Single(PartsOf(forcedOff));
        Assert.Equal(Script, PartsOf(forcedOff)[0].Text);
    }

    // ----- the script splitter -----

    [Fact]
    public void Split_DocsExampleWithAPreamble_DropsThePreambleAndTheEmptyTurnItCreates()
    {
        // The preamble ends in "Jane:", which looks like a marker. Its turn is empty, so it is skipped.
        var request = RequestFactory.CreateTextRequest(
            "TTS the following conversation between Joe and Jane: Joe: Hi Jane! Jane: Hello Joe!",
            new GeminiRequestOptions { SpeakerVoices = JoeAndJane });

        var parts = PartsOf(request);
        Assert.Equal(2, parts.Count);
        Assert.Equal("Hi Jane!", parts[0].Text);
        Assert.Equal("Hello Joe!", parts[1].Text);
    }

    [Fact]
    public void Split_MultiLineScript_KeepsInnerLineBreaksAndTrimsTheEnds()
    {
        var turns = Turns("Joe: First line.\nSecond line.\nJane:  Reply.  \n", ["Joe", "Jane"]);

        Assert.Equal(2, turns.Count);
        Assert.Equal(("Joe", "First line.\nSecond line."), turns[0]);
        Assert.Equal(("Jane", "Reply."), turns[1]);
    }

    [Fact]
    public void Split_AllowsSpacesBeforeTheColon_ButNotInsideAWord()
    {
        var turns = Turns("Joe : Hello. Joey: Not a marker. AJoe: Not one either.", ["Joe"]);

        Assert.Single(turns);
        Assert.Equal("Joe", turns[0].Speaker);
        Assert.Equal("Hello. Joey: Not a marker. AJoe: Not one either.", turns[0].Text);
    }

    [Fact]
    public void Split_MatchesNamesExactlyIncludingCase()
    {
        // "joe:" is not "Joe:", so no marker is found and the request is refused.
        Assert.Throws<ArgumentException>(() => Turns("joe: hello", ["Joe"]));
        Assert.Single(Turns("Joe: hello", ["Joe"]));
    }

    [Fact]
    public void Split_PrefixNames_PickTheRightSpeaker()
    {
        var turns = Turns("Annie: Hi. Ann: Hello.", ["Ann", "Annie"]);

        Assert.Equal(new[] { ("Annie", "Hi."), ("Ann", "Hello.") }, turns);
    }

    [Fact]
    public void Split_NamesWithRegexCharacters_AreTreatedAsLiteralText()
    {
        var turns = Turns("Dr. Who (host): Welcome. Guest [1]: Thanks.", ["Dr. Who (host)", "Guest [1]"]);

        Assert.Equal(new[] { ("Dr. Who (host)", "Welcome."), ("Guest [1]", "Thanks.") }, turns);
    }

    [Fact]
    public void Split_SameSpeakerTwiceInARow_StaysTwoTurns()
    {
        var turns = Turns("Joe: One. Joe: Two.", ["Joe"]);

        Assert.Equal(new[] { ("Joe", "One."), ("Joe", "Two.") }, turns);
    }

    [Fact]
    public void SpeakerVoices_WithNoMarkerInThePrompt_ThrowsAClearError()
    {
        var ex = Assert.Throws<ArgumentException>(() => RequestFactory.CreateTextRequest(
            "Hi Jane, hello Joe.",
            new GeminiRequestOptions { Model = "gemini-3.8-flash-tts", SpeakerVoices = JoeAndJane }));

        Assert.Contains("Joe, Jane", ex.Message);
        Assert.Contains("colon", ex.Message);
    }

    // ----- style -----

    [Fact]
    public void SpeechStyle_OnANewerModel_GoesInSpeechMetadataNotInTheText()
    {
        var request = RequestFactory.CreateTextRequest("Have a wonderful day.",
            new GeminiRequestOptions { Model = "gemini-3.8-flash-lite-tts", SpeechStyle = "cheerful" });

        var part = Assert.Single(PartsOf(request));
        Assert.Equal("Have a wonderful day.", part.Text);
        Assert.Equal("cheerful", part.SpeechMetadata!.Style);
        Assert.Null(part.SpeechMetadata.Speaker);
        Assert.Contains("\"speechMetadata\":{\"style\":\"cheerful\"}", Json(request));
    }

    [Fact]
    public void SpeechStyle_WithSpeakerVoices_AppliesToEveryTurn()
    {
        var request = RequestFactory.CreateTextRequest(Script,
            new GeminiRequestOptions { SpeakerVoices = JoeAndJane, SpeechStyle = "whispering" });

        Assert.All(PartsOf(request), p => Assert.Equal("whispering", p.SpeechMetadata!.Style));
        Assert.Equal(new[] { "Joe", "Jane" }, PartsOf(request).Select(p => p.SpeechMetadata!.Speaker));
    }

    [Fact]
    public void SpeechStyle_OnAnOlderModel_ThrowsInsteadOfSendingARequestTheApiWouldReject()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { Model = "gemini-2.5-flash-preview-tts", SpeechStyle = "cheerful" }));

        Assert.Contains("3.8", ex.Message);
        Assert.Contains("prompt", ex.Message);
    }

    [Fact]
    public void NoSpeechOptions_LeaveThePromptAsOneUntouchedPart()
    {
        var request = RequestFactory.CreateTextRequest(Script, new GeminiRequestOptions { Model = "gemini-3.8-flash-tts" });

        var part = Assert.Single(PartsOf(request));
        Assert.Equal(Script, part.Text);
        Assert.DoesNotContain("speechMetadata", Json(request));
    }

    // ----- voices, language, audio format -----

    [Theory]
    [InlineData("voice_abc123")]
    [InlineData("voicekey_abc123")]
    public void VoiceName_ThatIsACustomVoiceId_GoesInTheVoiceField(string id)
    {
        var request = RequestFactory.CreateTextRequest("hi", new GeminiRequestOptions { VoiceName = id });

        var json = Json(request);
        Assert.Contains($"\"speechConfig\":{{\"voiceConfig\":{{\"voice\":\"{id}\"}}}}", json);
        Assert.DoesNotContain("prebuiltVoiceConfig", json);
    }

    [Fact]
    public void VoiceName_ThatIsANamedVoice_StillGoesInPrebuiltVoiceConfig()
    {
        var json = Json(RequestFactory.CreateTextRequest("hi", new GeminiRequestOptions { VoiceName = "Kore" }));

        Assert.Contains("\"voiceConfig\":{\"prebuiltVoiceConfig\":{\"voiceName\":\"Kore\"}}", json);
        Assert.DoesNotContain("\"voice\":", json);
    }

    [Fact]
    public void SpeechLanguageCode_IsSentAlone_OrAlongsideAVoice()
    {
        var alone = Json(RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { SpeechLanguageCode = "en-US" }));
        var withVoice = Json(RequestFactory.CreateTextRequest("hi",
            new GeminiRequestOptions { SpeechLanguageCode = "en-US", VoiceName = "Kore" }));

        Assert.Contains("\"speechConfig\":{\"languageCode\":\"en-US\"}", alone);
        Assert.Contains("\"languageCode\":\"en-US\"", withVoice);
        Assert.Contains("\"prebuiltVoiceConfig\"", withVoice);
    }

    [Fact]
    public void AudioFormat_SerializesAsResponseFormat()
    {
        var json = Json(RequestFactory.CreateTextRequest("hi", new GeminiRequestOptions
        {
            AudioFormat = GeminiConstants.AudioFormats.Pcm16
        }));

        Assert.Contains("\"responseFormat\":{\"audio\":{\"mimeType\":\"AUDIO_L16\"}}", json);
        Assert.DoesNotContain("sampleRate", json); // the API ignores it, so the library does not send it
    }

    [Fact]
    public void NoAudioFormat_OmitsResponseFormat()
    {
        Assert.DoesNotContain("responseFormat", Json(RequestFactory.CreateTextRequest("hi", new GeminiRequestOptions())));
        Assert.DoesNotContain("responseFormat", Json(RequestFactory.CreateTextRequest("hi", null)));
    }

    [Fact]
    public void Clone_CopiesEveryNewSpeechOption()
    {
        var original = new GeminiRequestOptions
        {
            SpeechStyle = "calm",
            UseSpeechMetadata = true,
            AudioFormat = GeminiConstants.AudioFormats.Wav,
            SpeechLanguageCode = "fr-FR"
        };

        var copy = original.Clone();

        Assert.Equal("calm", copy.SpeechStyle);
        Assert.True(copy.UseSpeechMetadata);
        Assert.Equal("AUDIO_WAV", copy.AudioFormat);
        Assert.Equal("fr-FR", copy.SpeechLanguageCode);
    }

    // ----- audio output -----

    [Theory]
    [InlineData("audio/wav")]
    [InlineData("audio/x-wav")]
    [InlineData("AUDIO/WAV")]
    public void ToWav_WhenTheApiAlreadyReturnedWav_ReturnsTheBytesUnchanged(string mimeType)
    {
        // The Gemini 3.8 TTS models return a complete WAV file by default for a unary call. Adding a
        // second header would corrupt it.
        var wav = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4 };

        var result = new GeneratedAudio(mimeType, wav).ToWav();

        Assert.Equal(wav, result);
    }

    [Fact]
    public void ToWav_StillWrapsThe38StreamingFormat()
    {
        // audio/l16; rate=24000; channels=1 is what the 3.8 models return for a stream and for AUDIO_L16.
        var pcm = new byte[] { 9, 8, 7, 6 };

        var wav = new GeneratedAudio("audio/l16; rate=24000; channels=1", pcm).ToWav();

        Assert.Equal(44 + pcm.Length, wav.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(24000, BitConverter.ToInt32(wav, 24));
    }

    [Theory]
    [InlineData("audio/mulaw")]
    [InlineData("audio/alaw")]
    [InlineData("audio/mulaw; rate=24000; channels=1")] // the real strings from a live call on 2026-10-10
    [InlineData("audio/alaw; rate=24000; channels=1")]
    public void ToWav_OnTelephonyFormats_ThrowsRatherThanWrapTheWrongCodec(string mimeType)
    {
        Assert.Throws<FormatException>(() => new GeneratedAudio(mimeType, [1, 2]).ToWav());
    }

    // ----- models and constants -----

    [Fact]
    public void Defaults_PointAtTheCurrentModels()
    {
        Assert.Equal("gemini-3.8-flash-lite-tts", GeminiConstants.Models.RecommendedTts);
        Assert.Equal("gemini-3.8-flash-tts", GeminiConstants.Models.Gemini38FlashTts);
        Assert.Equal("gemini-nano-banana-2.1", GeminiConstants.Models.GeminiNanoBanana21);
        // Nano Banana 2.1 is opt-in. Its IMAGE_RECITATION filter blocked simple prompts in live tests.
        Assert.Equal("gemini-3.1-flash-image", GeminiConstants.Models.RecommendedImage);
        Assert.Equal("gemini-3.8-flash", GeminiConstants.Models.Recommended);
    }

    [Fact]
    public void ContentGenerationModels_NoLongerListsTheRedirectedModels()
    {
        Assert.DoesNotContain("gemini-3.5-flash", GeminiConstants.Models.ContentGenerationModels);
        Assert.DoesNotContain("gemini-3.7-flash", GeminiConstants.Models.ContentGenerationModels);
        Assert.Contains("gemini-3.8-flash", GeminiConstants.Models.ContentGenerationModels);
    }

    [Fact]
    public void ImageConstants_IncludeTheWideAndTallRatiosAndThe512Size()
    {
        Assert.Equal("1:4", GeminiConstants.ImageAspectRatios.Tall1x4);
        Assert.Equal("4:1", GeminiConstants.ImageAspectRatios.Wide4x1);
        Assert.Equal("1:8", GeminiConstants.ImageAspectRatios.Tall1x8);
        Assert.Equal("8:1", GeminiConstants.ImageAspectRatios.Wide8x1);
        Assert.Equal("512", GeminiConstants.ImageSizes.FiveTwelve);

        var json = Json(RequestFactory.CreateTextRequest("a strip", new GeminiRequestOptions
        {
            ImageAspectRatio = GeminiConstants.ImageAspectRatios.Tall1x8,
            ImageSize = GeminiConstants.ImageSizes.OneK
        }));
        Assert.Contains("\"imageConfig\":{\"aspectRatio\":\"1:8\",\"imageSize\":\"1K\"}", json);
    }

    // ----- function-call ids and media processing -----

    [Fact]
    public void FunctionCallId_IsReadFromTheResponse_AndEchoedInTheReply()
    {
        const string responseJson = """
            {"candidates":[{"content":{"role":"model","parts":[
              {"functionCall":{"id":"call_148005","name":"get_weather","args":{"city":"Paris"}}},
              {"functionCall":{"id":"call_148006","name":"get_weather","args":{"city":"London"}}}
            ]},"finishReason":"STOP"}]}
            """;
        var response = JsonSerializer.Deserialize<GenerateContentResponse>(responseJson, GeminiJson.Default)!;
        var calls = response.Candidates![0].Content!.Parts!.Select(p => p.FunctionCall!).ToList();

        Assert.Equal(new[] { "call_148005", "call_148006" }, calls.Select(c => c.Id));

        var reply = new Part
        {
            FunctionResponse = new FunctionResponsePart
            {
                Id = calls[0].Id,
                Name = calls[0].Name,
                Response = JsonNode.Parse("{\"tempC\":18}")
            }
        };
        var json = JsonSerializer.Serialize(reply, GeminiJson.Default);
        Assert.Contains("\"id\":\"call_148005\"", json);
    }

    [Fact]
    public void FunctionResponse_WithoutAnId_OmitsTheField()
    {
        var part = new Part { FunctionResponse = new FunctionResponsePart { Name = "get_weather" } };

        Assert.DoesNotContain("\"id\"", JsonSerializer.Serialize(part, GeminiJson.Default));
    }

    [Fact]
    public void MediaProcessing_SerializesOnThePart_AndIsOmittedWhenUnset()
    {
        var withValue = JsonSerializer.Serialize(
            new Part { MediaProcessing = GeminiConstants.MediaProcessing.Agentic }, GeminiJson.Default);
        var without = JsonSerializer.Serialize(new Part { Text = "hi" }, GeminiJson.Default);

        Assert.Contains("\"mediaProcessing\":\"AGENTIC\"", withValue);
        Assert.DoesNotContain("mediaProcessing", without);
    }
}
