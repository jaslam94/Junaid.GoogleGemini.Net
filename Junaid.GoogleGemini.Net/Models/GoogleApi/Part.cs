using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class Part
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    public InlineData? InlineData { get; set; }
}
