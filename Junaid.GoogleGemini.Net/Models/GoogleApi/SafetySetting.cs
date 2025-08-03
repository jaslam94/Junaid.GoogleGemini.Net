using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class SafetySetting
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("threshold")]
    public string Threshold { get; set; } = string.Empty;
}