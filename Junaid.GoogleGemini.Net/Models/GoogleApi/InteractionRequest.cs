using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// A request to the Interactions API (<c>POST /v1beta/interactions</c>). This is NOT a
/// <c>generateContent</c> request: it is a different endpoint with different field names
/// (<c>snake_case</c>, not <c>camelCase</c>), used today only for speech-to-text
/// (<see cref="Services.Interfaces.ITranscriptionService"/>). See <c>PLAN-stt.md</c> for the live
/// verification this shape was built from.
/// </summary>
public class InteractionRequest
{
    /// <summary>The model to call, e.g. <c>GeminiConstants.Models.Gemini35Transcribe</c>.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>The input items for this interaction. One audio item per call, for transcription.</summary>
    [JsonPropertyName("input")]
    public List<InteractionInput>? Input { get; set; }

    /// <summary>Generation options, e.g. transcription settings.</summary>
    [JsonPropertyName("generation_config")]
    public InteractionGenerationConfig? GenerationConfig { get; set; }

    /// <summary>Set to request the named-event SSE stream instead of one JSON response.</summary>
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}

/// <summary>One input item. For transcription, always <c>Type == "audio"</c>, with either
/// <see cref="Data"/> (inline base64) or <see cref="Uri"/> (a Files API URI) set, not both.</summary>
public class InteractionInput
{
    /// <summary>The input kind, e.g. <c>"audio"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Inline base64-encoded bytes. Mutually exclusive with <see cref="Uri"/>.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>A Files API URI (<see cref="FileResource.Uri"/>). Mutually exclusive with <see cref="Data"/>.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    /// <summary>The input's MIME type, e.g. <c>"audio/wav"</c>.</summary>
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}

/// <summary>Generation-time options for an <see cref="InteractionRequest"/>.</summary>
public class InteractionGenerationConfig
{
    /// <summary>Transcription-specific options. See <see cref="TranscriptionConfig"/>.</summary>
    [JsonPropertyName("transcription_config")]
    public TranscriptionConfig? TranscriptionConfig { get; set; }
}

/// <summary>
/// Transcription options, live-verified field by field against the real API (see <c>PLAN-stt.md</c>
/// §3.2/§3.5). <see cref="CustomVocabulary"/> is confirmed rejected by the API (HTTP 400) when
/// combined with <see cref="DiarizationMode"/> or <see cref="TimestampGranularities"/>; see
/// <see cref="Requests.TranscriptionOptions"/>, which validates this client-side before the call.
/// </summary>
public class TranscriptionConfig
{
    /// <summary>BCP-47 language hints, e.g. <c>["en-US"]</c>. Confirmed accepted; auto-detection
    /// applies when omitted.</summary>
    [JsonPropertyName("language_codes")]
    public List<string>? LanguageCodes { get; set; }

    /// <summary>Domain-specific terms to bias recognition toward. Confirmed incompatible with
    /// <see cref="DiarizationMode"/>/<see cref="TimestampGranularities"/> (real HTTP 400).</summary>
    [JsonPropertyName("custom_vocabulary")]
    public List<string>? CustomVocabulary { get; set; }

    /// <summary>
    /// Set to <c>"speaker"</c> for per-word speaker labels. Confirmed live: produces a zero-indexed
    /// <c>"spk:0"</c>/<c>"spk:1"</c>-style <see cref="InteractionAnnotation.Speaker"/> on each word
    /// annotation (not the "spk_1", one-indexed format some docs describe). Plain <c>string</c>, not
    /// an enum: this library does not lock request string values to an allow-list, see
    /// <c>GeminiConstants.Models</c>' own doc comment for why.
    /// </summary>
    [JsonPropertyName("diarization_mode")]
    public string? DiarizationMode { get; set; }

    /// <summary>e.g. <c>["word"]</c>. Confirmed live: produces real <c>start_offset</c>/<c>end_offset</c>
    /// values on each <see cref="InteractionAnnotation"/>. <c>"segment"</c> is also accepted by the API
    /// but its output was not confirmed to differ from <c>"word"</c> in this library's testing.</summary>
    [JsonPropertyName("timestamp_granularities")]
    public List<string>? TimestampGranularities { get; set; }
}
