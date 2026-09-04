using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Controls how the model generates content (sampling, limits, output format, and reasoning).
/// </summary>
public class GenerationConfig
{
    /// <summary>
    /// Sampling temperature. Higher = more random.
    /// <b>Deprecated by Google (July-August 2026):</b> ignored on <c>gemini-3.8-flash</c>,
    /// <c>gemini-3.7-flash</c>, <c>gemini-3.6-flash</c>, <c>gemini-3.5-flash-lite</c> and later model generations, which will
    /// reject it with HTTP 400 in a future release. Google's guidance for those models is to steer
    /// determinism via <c>SystemInstruction</c> instead of sampling params.
    /// </summary>
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    /// <summary>Top-k sampling. See the deprecation note on <see cref="Temperature"/>.</summary>
    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    /// <summary>Nucleus (top-p) sampling. See the deprecation note on <see cref="Temperature"/>.</summary>
    [JsonPropertyName("topP")]
    public float? TopP { get; set; }

    /// <summary>Maximum number of tokens to generate.</summary>
    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    /// <summary>Number of candidate responses to return.</summary>
    [JsonPropertyName("candidateCount")]
    public int? CandidateCount { get; set; }

    /// <summary>Sequences that stop generation when produced.</summary>
    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }

    /// <summary>
    /// Output MIME type. Set to <c>application/json</c> together with <see cref="ResponseSchema"/>
    /// to get structured JSON output.
    /// </summary>
    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; set; }

    /// <summary>
    /// A schema (OpenAPI subset) the JSON output must conform to. Used with
    /// <see cref="ResponseMimeType"/> = <c>application/json</c>.
    /// </summary>
    [JsonPropertyName("responseSchema")]
    public JsonNode? ResponseSchema { get; set; }

    /// <summary>Deterministic sampling seed.</summary>
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    /// <summary>
    /// Penalty for token presence (discourages repeating tokens).
    /// <b>Not just ignored on Gemini 3.x. Actively rejected.</b> Live-verified 2026-09-03 against
    /// <c>gemini-3.8-flash</c>: setting this returns HTTP 400 <c>INVALID_ARGUMENT</c>, "Penalty is not
    /// enabled for this model" (surfaces as
    /// <see cref="Junaid.GoogleGemini.Net.Exceptions.GeminiApiException"/>). This differs
    /// from <see cref="Temperature"/>/<see cref="TopK"/>/<see cref="TopP"/>, which are silently ignored
    /// on the same models, not rejected. Leave null for Gemini 3.x.
    /// </summary>
    [JsonPropertyName("presencePenalty")]
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Penalty scaled by token frequency. See the "actively rejected on Gemini 3.x" note on
    /// <see cref="PresencePenalty"/>. The same HTTP 400 applies here. Leave null for Gemini 3.x.
    /// </summary>
    [JsonPropertyName("frequencyPenalty")]
    public float? FrequencyPenalty { get; set; }

    /// <summary>Configures model "thinking"/reasoning (Gemini 2.5+).</summary>
    [JsonPropertyName("thinkingConfig")]
    public ThinkingConfig? ThinkingConfig { get; set; }

    /// <summary>
    /// Default media resolution for image/video/PDF parts (Gemini 3+). One of the
    /// <c>GeminiConstants.MediaResolutions</c> values; can also be set per-part.
    /// </summary>
    [JsonPropertyName("mediaResolution")]
    public string? MediaResolution { get; set; }

    /// <summary>
    /// Output modalities to request (e.g. <c>["TEXT","IMAGE"]</c> for image generation). One or more
    /// of the <c>GeminiConstants.ResponseModalities</c> values. Null lets the model use its default
    /// (text only).
    /// </summary>
    [JsonPropertyName("responseModalities")]
    public List<string>? ResponseModalities { get; set; }

    /// <summary>
    /// Image generation settings (Gemini 3+ image models). Only meaningful alongside
    /// <see cref="ResponseModalities"/> including <c>IMAGE</c>.
    /// </summary>
    [JsonPropertyName("imageConfig")]
    public ImageConfig? ImageConfig { get; set; }

    /// <summary>
    /// Voice settings for TTS models. Only meaningful alongside <see cref="ResponseModalities"/>
    /// including <c>AUDIO</c>. Set exactly one of <see cref="SpeechConfig.VoiceConfig"/> (single
    /// speaker) or <see cref="SpeechConfig.MultiSpeakerVoiceConfig"/> (a script with named speakers).
    /// </summary>
    [JsonPropertyName("speechConfig")]
    public SpeechConfig? SpeechConfig { get; set; }
}

/// <summary>Image generation settings for Gemini 3+ image models (<c>generationConfig.imageConfig</c>).</summary>
public class ImageConfig
{
    /// <summary>One of the <c>GeminiConstants.ImageAspectRatios</c> values (e.g. <c>"16:9"</c>).</summary>
    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; set; }

    /// <summary>One of the <c>GeminiConstants.ImageSizes</c> values (e.g. <c>"2K"</c>).</summary>
    [JsonPropertyName("imageSize")]
    public string? ImageSize { get; set; }
}

/// <summary>Voice settings for TTS models (<c>generationConfig.speechConfig</c>). Live-verified
/// against the real API on 2026-09-04, see <c>PLAN-tts.md</c>.</summary>
public class SpeechConfig
{
    /// <summary>Single-speaker voice. Set this for a plain text-to-speech request.</summary>
    [JsonPropertyName("voiceConfig")]
    public VoiceConfig? VoiceConfig { get; set; }

    /// <summary>Speaker-to-voice map for a multi-speaker script. Set this instead of
    /// <see cref="VoiceConfig"/> when the input text names more than one speaker.</summary>
    [JsonPropertyName("multiSpeakerVoiceConfig")]
    public MultiSpeakerVoiceConfig? MultiSpeakerVoiceConfig { get; set; }
}

/// <summary>Wraps a single named voice. See <c>GeminiConstants</c>'s remarks on TTS voice names for
/// why this library does not enumerate them as constants.</summary>
public class VoiceConfig
{
    [JsonPropertyName("prebuiltVoiceConfig")]
    public PrebuiltVoiceConfig? PrebuiltVoiceConfig { get; set; }
}

/// <summary>Names one of Google's built-in TTS voices, e.g. <c>"Kore"</c>. See Google's TTS voice
/// list for the current full set; this library does not duplicate it as constants (see
/// <c>PLAN-tts.md</c> for why).</summary>
public class PrebuiltVoiceConfig
{
    [JsonPropertyName("voiceName")]
    public string? VoiceName { get; set; }
}

/// <summary>The voice assignment for a multi-speaker TTS script.</summary>
public class MultiSpeakerVoiceConfig
{
    [JsonPropertyName("speakerVoiceConfigs")]
    public List<SpeakerVoiceConfig>? SpeakerVoiceConfigs { get; set; }
}

/// <summary>One speaker's voice assignment within a multi-speaker script. <see cref="Speaker"/> must
/// match a speaker name as it appears in the input text.</summary>
public class SpeakerVoiceConfig
{
    [JsonPropertyName("speaker")]
    public string? Speaker { get; set; }

    [JsonPropertyName("voiceConfig")]
    public VoiceConfig? VoiceConfig { get; set; }
}

/// <summary>Configures the model's internal reasoning budget (Gemini 2.5+ "thinking").</summary>
public class ThinkingConfig
{
    /// <summary>
    /// Reasoning token budget (Gemini 2.5). <c>0</c> disables thinking (where allowed); <c>-1</c> lets
    /// the model decide. Mutually exclusive with <see cref="ThinkingLevel"/>.
    /// </summary>
    [JsonPropertyName("thinkingBudget")]
    public int? ThinkingBudget { get; set; }

    /// <summary>
    /// Reasoning depth (Gemini 3+): one of the <c>GeminiConstants.ThinkingLevels</c> values
    /// ("minimal"/"low"/"medium"/"high"). Mutually exclusive with <see cref="ThinkingBudget"/>.
    /// </summary>
    [JsonPropertyName("thinkingLevel")]
    public string? ThinkingLevel { get; set; }

    /// <summary>When true, thought-summary parts are included in the response.</summary>
    [JsonPropertyName("includeThoughts")]
    public bool? IncludeThoughts { get; set; }
}
