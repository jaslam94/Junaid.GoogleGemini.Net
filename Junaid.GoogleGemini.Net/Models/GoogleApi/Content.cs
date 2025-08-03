using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class Content
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; } = new();
}
