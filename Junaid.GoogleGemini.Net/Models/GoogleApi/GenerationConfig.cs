using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Controls how the model generates content (sampling, limits, output format, and reasoning).
/// </summary>
public class GenerationConfig
{
    /// <summary>Sampling temperature. Higher = more random.</summary>
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    /// <summary>Top-k sampling.</summary>
    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    /// <summary>Nucleus (top-p) sampling.</summary>
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

    /// <summary>Penalty for token presence (discourages repeating tokens).</summary>
    [JsonPropertyName("presencePenalty")]
    public float? PresencePenalty { get; set; }

    /// <summary>Penalty scaled by token frequency.</summary>
    [JsonPropertyName("frequencyPenalty")]
    public float? FrequencyPenalty { get; set; }

    /// <summary>Configures model "thinking"/reasoning (Gemini 2.5+).</summary>
    [JsonPropertyName("thinkingConfig")]
    public ThinkingConfig? ThinkingConfig { get; set; }
}

/// <summary>Configures the model's internal reasoning budget (Gemini 2.5+ "thinking").</summary>
public class ThinkingConfig
{
    /// <summary>
    /// Reasoning token budget. <c>0</c> disables thinking (where allowed); <c>-1</c> lets the model
    /// decide dynamically.
    /// </summary>
    [JsonPropertyName("thinkingBudget")]
    public int? ThinkingBudget { get; set; }

    /// <summary>When true, thought-summary parts are included in the response.</summary>
    [JsonPropertyName("includeThoughts")]
    public bool? IncludeThoughts { get; set; }
}
