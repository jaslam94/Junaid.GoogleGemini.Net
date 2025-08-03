using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

public class GenerationConfig
{
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    [JsonPropertyName("topP")]
    public float? TopP { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("candidateCount")]
    public int? CandidateCount { get; set; }

    [JsonPropertyName("stopSequences")]
    public List<string>? StopSequences { get; set; }
}