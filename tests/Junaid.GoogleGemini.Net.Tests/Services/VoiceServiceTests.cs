using System.Net;
using System.Text;
using System.Text.Json;
using Junaid.GoogleGemini.Net.Extensions;
using Junaid.GoogleGemini.Net.Infrastructure;
using Junaid.GoogleGemini.Net.Models.Requests;
using Junaid.GoogleGemini.Net.Services;
using Junaid.GoogleGemini.Net.Services.Interfaces;
using Junaid.GoogleGemini.Net.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Junaid.GoogleGemini.Net.Tests.Services;

/// <summary>
/// The Voices API wrapper. The JSON below was copied from real responses on 2026-10-10: a prebuilt voice from a
/// list call, and the response to creating a voice with voice design (only the audio blob is shortened).
/// </summary>
public class VoiceServiceTests
{
    private const string CreateResponse = """
        {"id":"voice_fixture0001","model":"models/gemini-3.8-flash-tts","type":"prompted","expire_time":"2027-10-11T13:35:00.426050520Z","display_name":"Gemini.Net live test (delete me)","prompted":{"input":"A calm, friendly narrator in her thirties with a clear American accent."},"language_code":"en-US","gender":"female","usage":{"total_tokens":662,"total_input_tokens":211,"total_output_tokens":451,"total_thought_tokens":121},"sample_audio":{"mime_type":"audio/wav","data":"UklGRg=="}}
        """;

    private const string PrebuiltList = """
        {"voices":[{"id":"achernar","type":"prebuilt","display_name":"Achernar","language_code":"en-US","region_code":"US","accent":"General American","persona":"Storyteller & Narrator","context":"Content & Media","gender":"female","pitch":"high","description":"Soft, calm, and soothing voice with a higher pitch."}],"next_page_token":"ERh6R-3lB44tGAEiDGFjaGlyZHxlbi1VUw"}
        """;

    private static VoiceService CreateService(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1beta/") };
        var client = new GeminiClient(http, NullLogger<GeminiClient>.Instance, GeminiRateLimiter.CreateDisabled(), GeminiCostGovernor.CreateDisabled());
        return new VoiceService(client, NullLogger<VoiceService>.Instance);
    }

    // AbsoluteUri keeps the percent-escaping that goes on the wire. Uri.ToString() decodes it for display.
    private static string Url(FakeHttpMessageHandler handler, int index = 0) => handler.Requests[index].RequestUri!.AbsoluteUri;

    // ----- list -----

    [Fact]
    public async Task ListAsync_ParsesARealPrebuiltVoice_AndTheNextPageToken()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, PrebuiltList);

        var page = await CreateService(handler).ListAsync();

        var voice = Assert.Single(page.Voices!);
        Assert.Equal("achernar", voice.Id);
        Assert.Equal("prebuilt", voice.Type);
        Assert.Equal("Storyteller & Narrator", voice.Persona);
        Assert.Equal("Content & Media", voice.Context);
        Assert.Equal("General American", voice.Accent);
        Assert.Equal("high", voice.Pitch);
        Assert.Equal("US", voice.RegionCode);
        Assert.Null(voice.ExpireTime);
        Assert.Null(voice.ExpiresAt);
        Assert.Equal("ERh6R-3lB44tGAEiDGFjaGlyZHxlbi1VUw", page.NextPageToken);
        Assert.Equal("https://example.test/v1beta/voices", Url(handler));
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task ListAsync_SendsEachFilterWithGooglesSnakeCaseName_RepeatingListValues()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"voices":[]}""");

        await CreateService(handler).ListAsync(new VoiceListOptions
        {
            LanguageCodes = ["en-GB", "fr-FR"],
            RegionCodes = ["GB"],
            Accents = ["General American"],
            Genders = ["female"],
            Pitches = ["low"],
            Personas = ["Storyteller & Narrator"],
            Contexts = ["Content & Media"],
            Types = ["prompted", "replicated"],
            Search = "calm voice",
            PageSize = 25,
            PageToken = "tok/+=",
        });

        Assert.Equal(
            "https://example.test/v1beta/voices" +
            "?language_code=en-GB&language_code=fr-FR&region_code=GB&accent=General%20American&gender=female&pitch=low" +
            "&persona=Storyteller%20%26%20Narrator&context=Content%20%26%20Media&type=prompted&type=replicated" +
            "&search=calm%20voice&page_size=25&page_token=tok%2F%2B%3D",
            Url(handler));
    }

    [Fact]
    public async Task ListAsync_WithNoFilters_AddsNoQueryString_AndSkipsBlankValues()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"voices":[]}""");

        await CreateService(handler).ListAsync(new VoiceListOptions { Genders = [" ", ""], Search = "  ", PageSize = 0 });

        Assert.Equal("https://example.test/v1beta/voices", Url(handler));
    }

    [Fact]
    public async Task ListAllAsync_FollowsPageTokens_UntilThereAreNone()
    {
        var pages = new Queue<string>(
        [
            """{"voices":[{"id":"a"},{"id":"b"}],"next_page_token":"t1"}""",
            """{"voices":[{"id":"c"}],"next_page_token":"t2"}""",
            """{"voices":[{"id":"d"}]}""",
        ]);
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(HttpStatusCode.OK, pages.Dequeue()));

        var ids = new List<string?>();
        await foreach (var v in CreateService(handler).ListAllAsync(new VoiceListOptions { PageSize = 2 }))
        {
            ids.Add(v.Id);
        }

        Assert.Equal(new[] { "a", "b", "c", "d" }, ids);
        Assert.Equal(3, handler.CallCount);
        Assert.DoesNotContain("page_token", Url(handler, 0));
        Assert.Contains("page_token=t1", Url(handler, 1));
        Assert.Contains("page_token=t2", Url(handler, 2));
        Assert.All(Enumerable.Range(0, 3), i => Assert.Contains("page_size=2", Url(handler, i)));
    }

    [Fact]
    public async Task ListAllAsync_StopsIfGoogleRepeatsATokenForever()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, """{"voices":[{"id":"x"}],"next_page_token":"same"}""");

        var count = 0;
        await foreach (var _ in CreateService(handler).ListAllAsync())
        {
            count++;
        }

        Assert.Equal(2, count); // the first page, then one more page that repeats the token, then stop
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ListAllAsync_DoesNotChangeTheCallersOptions()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK, handlerCalls++ == 0 ? """{"voices":[],"next_page_token":"t1"}""" : """{"voices":[]}"""));
        var options = new VoiceListOptions { PageToken = "start" };

        await foreach (var _ in CreateService(handler).ListAllAsync(options)) { }

        Assert.Equal("start", options.PageToken);
    }

    private int handlerCalls;

    // ----- get -----

    [Fact]
    public async Task GetAsync_ParsesTheRealCreateShape_IncludingTheSampleAndTheExpiry()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        var voice = await CreateService(handler).GetAsync("voice_fixture0001");

        Assert.Equal("https://example.test/v1beta/voices/voice_fixture0001", Url(handler));
        Assert.Equal("voice_fixture0001", voice.Id);
        Assert.Equal("prompted", voice.Type);
        Assert.Equal("models/gemini-3.8-flash-tts", voice.Model);
        Assert.Equal("A calm, friendly narrator in her thirties with a clear American accent.", voice.Prompted!.Input);
        Assert.Equal("female", voice.Gender);
        Assert.Equal("en-US", voice.LanguageCode);
        Assert.Equal("audio/wav", voice.SampleAudio!.MimeType);
        Assert.Equal(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }, voice.SampleAudio.GetBytes());
        Assert.Equal(662, voice.Usage!["total_tokens"]!.GetValue<int>());

        // Google sends nine fractional digits. A parser that only knows seven would fail on this exact string.
        var expires = voice.ExpiresAt!.Value;
        Assert.Equal(new DateTimeOffset(2027, 10, 11, 13, 35, 0, TimeSpan.Zero),
            new DateTimeOffset(expires.Year, expires.Month, expires.Day, expires.Hour, expires.Minute, expires.Second, expires.Offset));
    }

    [Theory]
    [InlineData("voice_abc", "voices/voice_abc")]
    [InlineData("voices/voice_abc", "voices/voice_abc")]
    public async Task GetAsync_AcceptsAnIdOrAFullResourceName(string given, string path)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        await CreateService(handler).GetAsync(given);

        Assert.Equal("https://example.test/v1beta/" + path, Url(handler));
    }

    [Fact]
    public async Task GetAsync_OnAnUnknownVoice_ThrowsTheApiError()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.NotFound,
            """{"error":{"code":404,"message":"The voice was not found or the caller does not have permission to access it.","status":"NOT_FOUND"}}""");

        var ex = await Assert.ThrowsAsync<Junaid.GoogleGemini.Net.Exceptions.GeminiApiException>(
            () => CreateService(handler).GetAsync("voice_nope"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Contains("not found", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAndDelete_RejectABlankId_BeforeAnyRequest(string id)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteAsync(id));
        Assert.Equal(0, handler.CallCount);
    }

    // ----- create: voice design -----

    [Fact]
    public async Task CreateDesignedAsync_SendsTheShapeThatWorkedAgainstTheRealApi()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        var voice = await CreateService(handler).CreateDesignedAsync(new DesignedVoiceRequest
        {
            Description = "A calm, friendly narrator in her thirties with a clear American accent.",
            DisplayName = "Gemini.Net live test (delete me)",
            Gender = "female",
            LanguageCode = "en-US",
        });

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://example.test/v1beta/voices", Url(handler));
        Assert.Equal(
            """{"store":true,"voice":{"type":"prompted","display_name":"Gemini.Net live test (delete me)","gender":"female","language_code":"en-US","prompted":{"input":"A calm, friendly narrator in her thirties with a clear American accent."}}}""",
            handler.RequestBodies[0]);
        Assert.Equal("voice_fixture0001", voice.Id);
    }

    [Fact]
    public async Task CreateDesignedAsync_LeavesTheModelOut_UnlessTheCallerNamesOne()
    {
        // The Lite TTS model refused voice design, so the default must be "no model".
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);
        var service = CreateService(handler);

        await service.CreateDesignedAsync(new DesignedVoiceRequest { Description = "A calm narrator." });
        await service.CreateDesignedAsync(new DesignedVoiceRequest { Description = "A calm narrator.", Model = "gemini-3.8-flash-tts" });

        Assert.DoesNotContain("\"model\"", handler.RequestBodies[0]);
        Assert.Contains("\"model\":\"gemini-3.8-flash-tts\"", handler.RequestBodies[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateDesignedAsync_RequiresADescription_BeforeAnyRequest(string description)
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService(handler).CreateDesignedAsync(new DesignedVoiceRequest { Description = description }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService(handler).CreateDesignedAsync(null!));
        Assert.Equal(0, handler.CallCount);
    }

    // ----- create: voice replication (Google's documented shape; not run against the real API) -----

    [Fact]
    public async Task CreateReplicatedAsync_SendsBothRecordingsAsBase64_InGooglesDocumentedShape()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        await CreateService(handler).CreateReplicatedAsync(new ReplicatedVoiceRequest
        {
            SourceAudio = Encoding.ASCII.GetBytes("source"),
            ConsentAudio = Encoding.ASCII.GetBytes("consent"),
            DisplayName = "My own voice",
        });

        Assert.Equal(
            """{"store":true,"voice":{"type":"replicated","display_name":"My own voice","replicated":{"source_audio":{"mime_type":"audio/wav","data":"c291cmNl"},"consent_audio":{"mime_type":"audio/wav","data":"Y29uc2VudA=="}}}}""",
            handler.RequestBodies[0]);
    }

    [Fact]
    public async Task CreateReplicatedAsync_RefusesWithoutConsentAudio_AndNeverInventsOne()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, CreateResponse);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CreateService(handler).CreateReplicatedAsync(
            new ReplicatedVoiceRequest { SourceAudio = [1, 2, 3] }));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(handler).CreateReplicatedAsync(
            new ReplicatedVoiceRequest { ConsentAudio = [1, 2, 3] }));

        Assert.Contains("Consent audio is required", ex.Message);
        Assert.Equal(0, handler.CallCount);
    }

    // ----- delete and registration -----

    [Fact]
    public async Task DeleteAsync_SendsADelete_ToTheVoice()
    {
        var handler = FakeHttpMessageHandler.RespondWith(HttpStatusCode.OK, "{}");

        await CreateService(handler).DeleteAsync("voice_fixture0001");

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("https://example.test/v1beta/voices/voice_fixture0001", Url(handler));
    }

    [Fact]
    public void AddGemini_RegistersTheVoiceService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGemini(o => o.ApiKey = "AIzaSyDUMMY_KEY_FOR_UNIT_TESTS_12345");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<VoiceService>(provider.GetRequiredService<IVoiceService>());
    }

    [Fact]
    public void TheVoiceModels_RoundTripThroughTheLibrariesSerializer()
    {
        // The Voices API is snake_case. This guards the source-generated context and the explicit names.
        var voice = JsonSerializer.Deserialize<Junaid.GoogleGemini.Net.Models.GoogleApi.Voice>(
            CreateResponse, Junaid.GoogleGemini.Net.Infrastructure.Serialization.GeminiJson.Default)!;
        var json = JsonSerializer.Serialize(voice, Junaid.GoogleGemini.Net.Infrastructure.Serialization.GeminiJson.Default);

        Assert.Contains("\"display_name\":\"Gemini.Net live test (delete me)\"", json);
        Assert.Contains("\"language_code\":\"en-US\"", json);
        Assert.Contains("\"sample_audio\":{\"mime_type\":\"audio/wav\"", json);
        Assert.DoesNotContain("displayName", json);
    }
}
