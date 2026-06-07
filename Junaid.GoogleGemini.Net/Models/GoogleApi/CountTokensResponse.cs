using System.Text.Json.Serialization;

namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Response model for the <c>countTokens</c> endpoint.
/// </summary>
public class CountTokensResponse
{
    /// <summary>Total number of tokens in the supplied content.</summary>
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }
}
