using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

// The Voices API uses snake_case, unlike the rest of the Gemini API, so every property here names its JSON
// field explicitly. Shapes were read from live responses on 2026-10-10. See PLAN-tts-3-8.md.

/// <summary>
/// A text-to-speech voice: one of Google's prebuilt voices, or a custom voice that you made with voice
/// design or voice replication. Pass <see cref="Id"/> as <c>GeminiRequestOptions.VoiceName</c> to speak with it.
/// </summary>
public class Voice
{
    /// <summary>
    /// The voice id. A prebuilt voice has a short name such as <c>"achernar"</c>. A stored custom voice
    /// has an id that starts with <c>voice_</c>.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The model that made a custom voice, for example <c>"models/gemini-3.8-flash-tts"</c>.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary><c>"prebuilt"</c>, <c>"prompted"</c> (voice design) or <c>"replicated"</c> (voice replication).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// When a stored custom voice expires, as Google sends it (RFC 3339, with up to nine fractional digits).
    /// Stored voices live for one year. Null for a prebuilt voice. See <see cref="ExpiresAt"/>.
    /// </summary>
    [JsonPropertyName("expire_time")]
    public string? ExpireTime { get; set; }

    /// <summary><see cref="ExpireTime"/> parsed, or null when there is none or it cannot be read.</summary>
    [JsonIgnore]
    public DateTimeOffset? ExpiresAt =>
        DateTimeOffset.TryParse(ExpireTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>A human-readable name.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    /// <summary>A short description. Set on prebuilt voices.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>A BCP-47 language tag, for example <c>"en-US"</c>.</summary>
    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; set; }

    /// <summary>A region code such as <c>"US"</c>. Set on prebuilt voices.</summary>
    [JsonPropertyName("region_code")]
    public string? RegionCode { get; set; }

    /// <summary>A regional accent, for example <c>"General American"</c>. Set on prebuilt voices.</summary>
    [JsonPropertyName("accent")]
    public string? Accent { get; set; }

    /// <summary>A character archetype, for example <c>"Storyteller &amp; Narrator"</c>. Set on prebuilt voices.</summary>
    [JsonPropertyName("persona")]
    public string? Persona { get; set; }

    /// <summary>The usage domain the voice suits, for example <c>"Content &amp; Media"</c>. Set on prebuilt voices.</summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    /// <summary><c>"female"</c>, <c>"male"</c> or <c>"neutral"</c>.</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    /// <summary><c>"low"</c>, <c>"medium"</c> or <c>"high"</c>. Set on prebuilt voices.</summary>
    [JsonPropertyName("pitch")]
    public string? Pitch { get; set; }

    /// <summary>The description a voice-design voice was made from. Set when <see cref="Type"/> is <c>"prompted"</c>.</summary>
    [JsonPropertyName("prompted")]
    public VoicePrompted? Prompted { get; set; }

    /// <summary>
    /// A short preview clip. Google returns it when you create a voice-design voice or get one by id. It is
    /// not in list results.
    /// </summary>
    [JsonPropertyName("sample_audio")]
    public VoiceSampleAudio? SampleAudio { get; set; }

    /// <summary>
    /// Token usage of the call that made the voice, in Google's raw shape. Present on a create response.
    /// The library keeps it as JSON because this is not the <c>generateContent</c> usage shape. It does
    /// not feed cost governance. A designed voice used 662 tokens when we tried it.
    /// </summary>
    [JsonPropertyName("usage")]
    public JsonNode? Usage { get; set; }
}

/// <summary>The text description a voice-design voice was made from.</summary>
public class VoicePrompted
{
    /// <summary>The description, for example "A calm narrator with a clear American accent."</summary>
    [JsonPropertyName("input")]
    public string? Input { get; set; }
}

/// <summary>A voice's preview clip.</summary>
public class VoiceSampleAudio
{
    /// <summary>The clip's MIME type. Live-verified as <c>"audio/wav"</c>.</summary>
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    /// <summary>The clip, base64-encoded as Google sends it. See <see cref="GetBytes"/>.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>The decoded clip. For <c>"audio/wav"</c> these bytes are a complete WAV file.</summary>
    public byte[] GetBytes() => string.IsNullOrEmpty(Data) ? Array.Empty<byte>() : Convert.FromBase64String(Data);
}

/// <summary>One page of voices from a list call.</summary>
public class VoiceList
{
    /// <summary>
    /// The voices on this page. Your stored custom voices come first (newest first), followed by the
    /// prebuilt voices that match the filters.
    /// </summary>
    [JsonPropertyName("voices")]
    public List<Voice>? Voices { get; set; }

    /// <summary>Pass this as <c>VoiceListOptions.PageToken</c> for the next page. Null on the last page.</summary>
    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; set; }
}

// ----- wire types for POST /v1beta/voices. The service builds these from the request classes. -----

/// <summary>The body of <c>POST /v1beta/voices</c>.</summary>
public class CreateVoiceRequest
{
    /// <summary>True to store the voice in the project and get a <c>voice_...</c> id.</summary>
    [JsonPropertyName("store")]
    public bool Store { get; set; } = true;

    /// <summary>The voice to create.</summary>
    [JsonPropertyName("voice")]
    public CreateVoiceBody? Voice { get; set; }
}

/// <summary>The <c>voice</c> object inside <see cref="CreateVoiceRequest"/>.</summary>
public class CreateVoiceBody
{
    /// <summary>Leave null. The API picks a default model. Voice design refused <c>gemini-3.8-flash-lite-tts</c>.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary><c>"prompted"</c> or <c>"replicated"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>A human-readable name.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    /// <summary><c>"female"</c>, <c>"male"</c> or <c>"neutral"</c>.</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    /// <summary>A BCP-47 language tag.</summary>
    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; set; }

    /// <summary>Set for <c>"prompted"</c> voices.</summary>
    [JsonPropertyName("prompted")]
    public VoicePrompted? Prompted { get; set; }

    /// <summary>Set for <c>"replicated"</c> voices.</summary>
    [JsonPropertyName("replicated")]
    public CreateVoiceReplicated? Replicated { get; set; }
}

/// <summary>The two recordings that voice replication needs.</summary>
public class CreateVoiceReplicated
{
    /// <summary>A 10 to 30 second clip of the speaker.</summary>
    [JsonPropertyName("source_audio")]
    public VoiceSampleAudio? SourceAudio { get; set; }

    /// <summary>The same speaker reciting Google's consent statement.</summary>
    [JsonPropertyName("consent_audio")]
    public VoiceSampleAudio? ConsentAudio { get; set; }
}
