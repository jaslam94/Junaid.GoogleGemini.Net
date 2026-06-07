using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>Metadata describing a Gemini model, as returned by <c>models.get</c> / <c>models.list</c>.</summary>
public class ModelInfo
{
    /// <summary>Resource name, e.g. <c>models/gemini-2.5-pro</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Model version string.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Human-readable display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>Description of the model.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Maximum number of input tokens supported.</summary>
    [JsonPropertyName("inputTokenLimit")]
    public int InputTokenLimit { get; set; }

    /// <summary>Maximum number of output tokens supported.</summary>
    [JsonPropertyName("outputTokenLimit")]
    public int OutputTokenLimit { get; set; }

    /// <summary>The generation methods this model supports (e.g. "generateContent", "embedContent").</summary>
    [JsonPropertyName("supportedGenerationMethods")]
    public string[]? SupportedGenerationMethods { get; set; }

    /// <summary>Default sampling temperature.</summary>
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    /// <summary>Default nucleus-sampling value.</summary>
    [JsonPropertyName("topP")]
    public double TopP { get; set; }

    /// <summary>Default top-k value.</summary>
    [JsonPropertyName("topK")]
    public int TopK { get; set; }
}
