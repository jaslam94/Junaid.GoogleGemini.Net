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
    /// <b>Deprecated by Google (July-August 2026):</b> ignored on <c>gemini-3.7-flash</c>,
    /// <c>gemini-3.6-flash</c>, <c>gemini-3.5-flash-lite</c> and later model generations, which will
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
    /// <b>Not just ignored on Gemini 3.x — actively rejected.</b> Live-verified 2026-09-03 against
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
    /// <see cref="PresencePenalty"/> — the same HTTP 400 applies here. Leave null for Gemini 3.x.
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
