namespace Junaid.GoogleGemini.Net.Models.GoogleApi;

/// <summary>
/// Legacy request configuration - DEPRECATED
/// Use GeminiRequestOptions instead
/// </summary>
[Obsolete("This class is deprecated. Use GeminiRequestOptions instead. Will be removed in v7.0.0")]
public class GenerateContentConfiguration
{
    /// <summary>
    /// DEPRECATED: Use GeminiRequestOptions.SafetySettings instead
    /// </summary>
    [Obsolete("Use GeminiRequestOptions.SafetySettings instead")]
    public SafetySetting[] safetySettings { get; set; }

    /// <summary>
    /// DEPRECATED: Use GeminiRequestOptions properties (Temperature, MaxTokens, etc.) instead
    /// </summary>
    [Obsolete("Use GeminiRequestOptions properties (Temperature, MaxTokens, TopP, TopK, StopSequences) instead")]
    public GenerationConfig generationConfig { get; set; }
}