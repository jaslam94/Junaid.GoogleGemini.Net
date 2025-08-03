using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class GenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("safetySettings")]
    public List<SafetySetting>? SafetySettings { get; set; }
}