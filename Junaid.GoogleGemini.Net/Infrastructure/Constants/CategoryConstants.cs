using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Constants
{
    /// <summary>
    /// DEPRECATED: Use GeminiConstants.SafetyCategories instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.SafetyCategories instead. Will be removed in v7.0.0")]
    public static class CategoryConstants
    {
        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.DangerousContent instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.DangerousContent instead")]
        public const string DangerousContent = GeminiConstants.SafetyCategories.DangerousContent;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.HateSpeech instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.HateSpeech instead")]
        public const string HateSpeech = GeminiConstants.SafetyCategories.HateSpeech;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.SexuallyExplicit instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.SexuallyExplicit instead")]
        public const string SexuallyExplicit = GeminiConstants.SafetyCategories.SexuallyExplicit;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.Harassment instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.Harassment instead")]
        public const string CategoryHarassment = GeminiConstants.SafetyCategories.Harassment;
    }
}