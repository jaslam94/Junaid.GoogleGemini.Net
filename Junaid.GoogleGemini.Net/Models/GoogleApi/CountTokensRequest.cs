namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Request model for counting tokens in content
/// </summary>
public class CountTokensRequest
{
    /// <summary>
    /// The content to count tokens for
    /// </summary>
    public List<Content> contents { get; set; } = new();
}