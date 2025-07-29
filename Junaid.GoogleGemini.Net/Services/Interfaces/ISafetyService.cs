using Junaid.GoogleGemini.Net.Models.GoogleApi;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Service for managing content safety settings and analyzing safety ratings
    /// </summary>
    public interface ISafetyService
    {
        /// <summary>
        /// Creates safety settings with specified thresholds for all available categories
        /// </summary>
        /// <param name="threshold">The safety threshold to apply to all categories</param>
        List<SafetySetting> CreateSafetySettings(string threshold);

        /// <summary>
        /// Creates safety settings with custom thresholds for specific categories
        /// </summary>
        /// <param name="settings">Dictionary mapping safety categories to their thresholds</param>
        List<SafetySetting> CreateSafetySettings(Dictionary<string, string> settings);

        /// <summary>
        /// Analyzes safety ratings from a response and returns detailed information
        /// </summary>
        /// <param name="response">The API response containing safety ratings</param>
        /// <returns>Dictionary mapping categories to their safety probabilities</returns>
        Dictionary<string, string> AnalyzeSafetyRatings(GenerateContentResponse response);

        /// <summary>
        /// Checks if content is safe based on specified thresholds
        /// </summary>
        /// <param name="response">The API response containing safety ratings</param>
        /// <param name="thresholds">Dictionary mapping categories to their minimum acceptable thresholds</param>
        /// <returns>True if content meets all safety thresholds, false otherwise</returns>
        bool IsContentSafe(GenerateContentResponse response, Dictionary<string, string> thresholds);

        /// <summary>
        /// Creates safety settings optimized for sensitive content handling
        /// </summary>
        List<SafetySetting> CreateStrictSafetySettings();

        /// <summary>
        /// Creates safety settings optimized for general content
        /// </summary>
        List<SafetySetting> CreateModerateSafetySettings();

        /// <summary>
        /// Creates safety settings with minimal restrictions
        /// </summary>
        List<SafetySetting> CreatePermissiveSafetySettings();
    }
}