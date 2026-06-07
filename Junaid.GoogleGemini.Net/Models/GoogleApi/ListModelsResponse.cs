using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Response containing a list of available models.
/// </summary>
public class ListModelsResponse
{
    /// <summary>The available models.</summary>
    [JsonPropertyName("models")]
    public ModelInfo[] Models { get; set; } = [];

    /// <summary>Token for fetching the next page of results, if any.</summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
