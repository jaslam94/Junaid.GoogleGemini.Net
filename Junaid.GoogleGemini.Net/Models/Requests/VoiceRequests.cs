namespace Junaid.GoogleGemini.Net.Models.Requests;

/// <summary>
/// Filters and paging for <c>IVoiceService.ListAsync</c>. Every filter that takes a list matches a voice
/// that has any of the values (OR). Different filters combine with AND. Values must match exactly:
/// <c>Personas = ["Narrator"]</c> finds nothing, because the real persona is "Storyteller &amp; Narrator".
/// Read the exact values from a list result first. Live-verified on 2026-10-10.
/// </summary>
public class VoiceListOptions
{
    /// <summary>BCP-47 language tags, for example <c>["en-US", "en-GB"]</c>.</summary>
    public IList<string>? LanguageCodes { get; set; }

    /// <summary>Region codes, for example <c>["US", "GB"]</c>.</summary>
    public IList<string>? RegionCodes { get; set; }

    /// <summary>Accent descriptors, for example <c>["General American"]</c>.</summary>
    public IList<string>? Accents { get; set; }

    /// <summary><c>"female"</c>, <c>"male"</c> or <c>"neutral"</c>.</summary>
    public IList<string>? Genders { get; set; }

    /// <summary><c>"low"</c>, <c>"medium"</c> or <c>"high"</c>.</summary>
    public IList<string>? Pitches { get; set; }

    /// <summary>Persona descriptors, for example <c>["Storyteller &amp; Narrator"]</c>.</summary>
    public IList<string>? Personas { get; set; }

    /// <summary>Usage domains, for example <c>["Content &amp; Media"]</c>.</summary>
    public IList<string>? Contexts { get; set; }

    /// <summary>Voice sources: <c>"prebuilt"</c>, <c>"prompted"</c> or <c>"replicated"</c>. Use <c>["prompted", "replicated"]</c> for your own voices.</summary>
    public IList<string>? Types { get; set; }

    /// <summary>A case-insensitive substring to find in the name and description.</summary>
    public string? Search { get; set; }

    /// <summary>The most voices per page. Google's default is 50 and its maximum is 1000.</summary>
    public int? PageSize { get; set; }

    /// <summary>The <c>NextPageToken</c> of the previous page.</summary>
    public string? PageToken { get; set; }

    /// <summary>Creates a shallow copy.</summary>
    public VoiceListOptions Clone() => (VoiceListOptions)MemberwiseClone();
}

/// <summary>
/// Describes a voice to make with voice design: you write the voice in words, and Google makes it.
/// </summary>
public class DesignedVoiceRequest
{
    /// <summary>
    /// The voice, in words. Required. For example "A calm, friendly narrator in her thirties with a clear
    /// American accent." Google advises one or two clear sentences.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>A name for the voice. Optional.</summary>
    public string? DisplayName { get; set; }

    /// <summary><c>"female"</c>, <c>"male"</c> or <c>"neutral"</c>. Optional.</summary>
    public string? Gender { get; set; }

    /// <summary>A BCP-47 language tag such as <c>"en-US"</c>. Optional.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// Leave this null. Live-verified on 2026-10-10: the API refused <c>gemini-3.8-flash-lite-tts</c> for voice
    /// design ("does not support Prompted Voice Design"), although Google's page says it does. With no model,
    /// the API picks a default, and the voice then works with either 3.8 TTS model.
    /// </summary>
    public string? Model { get; set; }
}

/// <summary>
/// Describes a voice to copy from two recordings of a real person. <b>Only use recordings of a speaker
/// who has agreed.</b> Google requires the consent recording, and this library does not check or fake it.
/// </summary>
public class ReplicatedVoiceRequest
{
    /// <summary>A 10 to 30 second clip of clean speech from the speaker. Required. Google recommends 24 kHz mono 16-bit WAV.</summary>
    public byte[] SourceAudio { get; set; } = Array.Empty<byte>();

    /// <summary>The MIME type of <see cref="SourceAudio"/>. Defaults to <c>"audio/wav"</c>.</summary>
    public string SourceAudioMimeType { get; set; } = "audio/wav";

    /// <summary>
    /// The same speaker reciting Google's consent statement, for example "I am the owner of this voice and I
    /// consent to Google using this voice to create a synthetic voice model." Required. See Google's voice
    /// replication page for the statement in each supported language.
    /// </summary>
    public byte[] ConsentAudio { get; set; } = Array.Empty<byte>();

    /// <summary>The MIME type of <see cref="ConsentAudio"/>. Defaults to <c>"audio/wav"</c>.</summary>
    public string ConsentAudioMimeType { get; set; } = "audio/wav";

    /// <summary>A name for the voice. Optional.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Leave this null unless you know you need a specific model. See <see cref="DesignedVoiceRequest.Model"/>.</summary>
    public string? Model { get; set; }
}
