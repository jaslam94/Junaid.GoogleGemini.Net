using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Response wrapper holding model metadata entries.
/// </summary>
public class ListModelInfoResponse
{
    /// <summary>The model metadata entries.</summary>
    [JsonPropertyName("models")]
    public ModelInfo[] Models { get; set; } = [];
}
