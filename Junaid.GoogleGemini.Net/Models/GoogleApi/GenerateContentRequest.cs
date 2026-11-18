using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class GenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; } = new();

    /// <summary>
    /// Optional system-level guidance applied to the whole request (tone, role, rules). Text only.
    /// </summary>
    [JsonPropertyName("systemInstruction")]
    public Content? SystemInstruction { get; set; }

    /// <summary>Tools the model may use (function declarations and/or built-in tools).</summary>
    [JsonPropertyName("tools")]
    public List<Tool>? Tools { get; set; }

    /// <summary>Controls how the model is allowed to call functions.</summary>
    [JsonPropertyName("toolConfig")]
    public ToolConfig? ToolConfig { get; set; }

    [JsonPropertyName("generationConfig")]
    public GenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("safetySettings")]
    public List<SafetySetting>? SafetySettings { get; set; }

    /// <summary>
    /// Name of a cached-content resource (e.g. <c>cachedContents/abc</c>) to prepend to this request.
    /// </summary>
    [JsonPropertyName("cachedContent")]
    public string? CachedContent { get; set; }

    /// <summary>
    /// Whether Google may store this request and its response. <c>null</c> sends nothing, so the
    /// project setting applies. <c>false</c> opts this request out. <c>true</c> opts it in.
    /// </summary>
    [JsonPropertyName("store")]
    public bool? Store { get; set; }
}