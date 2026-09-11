using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Token usage for an <see cref="Interaction"/>. A different shape than <c>generateContent</c>'s
/// <see cref="UsageMetadata"/> (different field names, and one confirmed quirk, see
/// <see cref="TotalOutputTokens"/>), live-verified in <c>PLAN-stt.md</c> §3.3.
/// </summary>
public class InteractionUsage
{
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("total_input_tokens")]
    public int TotalInputTokens { get; set; }

    /// <summary>Per-modality breakdown of input tokens, e.g. audio vs. text.</summary>
    [JsonPropertyName("input_tokens_by_modality")]
    public List<InteractionModalityTokenCount>? InputTokensByModality { get; set; }

    [JsonPropertyName("total_cached_tokens")]
    public int TotalCachedTokens { get; set; }

    /// <summary>
    /// CONFIRMED LIVE (<c>PLAN-stt.md</c> §3.3): this read <c>0</c> in every one of nine separate live
    /// calls made while building this feature, including calls that clearly produced real output
    /// text. <see cref="TotalTokens"/> also excludes output entirely (it equals <see cref="TotalInputTokens"/>
    /// exactly in every call observed). The real output token count is in
    /// <see cref="ModelInvocationTokenCounts"/>. Do not use this field for cost governance; it will
    /// silently under-report.
    /// </summary>
    [JsonPropertyName("total_output_tokens")]
    public int TotalOutputTokens { get; set; }

    [JsonPropertyName("total_tool_use_tokens")]
    public int TotalToolUseTokens { get; set; }

    [JsonPropertyName("total_thought_tokens")]
    public int TotalThoughtTokens { get; set; }

    [JsonPropertyName("raw_prompt_token")]
    public int RawPromptToken { get; set; }

    /// <summary>Per-model-call token breakdown. This is where the real output token count lives; see
    /// the warning on <see cref="TotalOutputTokens"/>.</summary>
    [JsonPropertyName("model_invocation_token_counts")]
    public List<ModelInvocationTokenCount>? ModelInvocationTokenCounts { get; set; }
}

/// <summary>One entry in an <see cref="InteractionUsage"/> per-modality token breakdown. Not the
/// same wire shape as <c>generateContent</c>'s <see cref="ModalityTokenCount"/> (that type's JSON
/// field is <c>"tokenCount"</c>; this one's is <c>"tokens"</c>), so it needs its own type.</summary>
public class InteractionModalityTokenCount
{
    [JsonPropertyName("modality")]
    public string? Modality { get; set; }

    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }
}

/// <summary>Token detail for one model invocation within an interaction. See the warning on
/// <see cref="InteractionUsage.TotalOutputTokens"/> for why <see cref="CandidatesTokensDetails"/>,
/// not the usage object's top-level fields, is the real source of the output token count.</summary>
public class ModelInvocationTokenCount
{
    [JsonPropertyName("prompt_tokens_details")]
    public List<InteractionModalityTokenCount>? PromptTokensDetails { get; set; }

    /// <summary>The real per-invocation output token count, summed across entries here.</summary>
    [JsonPropertyName("candidates_tokens_details")]
    public List<InteractionModalityTokenCount>? CandidatesTokensDetails { get; set; }
}
