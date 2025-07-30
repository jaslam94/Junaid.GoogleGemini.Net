using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Constants
{
    /// <summary>
    /// DEPRECATED: Use GeminiConstants.SafetyCategories and GeminiConstants.SafetyThresholds instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.SafetyCategories and GeminiConstants.SafetyThresholds instead. Will be removed in v7.0.0")]
    public static class SafetyCategory
    {
        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.Harassment instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.Harassment instead")]
        public const string Harassment = GeminiConstants.SafetyCategories.Harassment;

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
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.DangerousContent instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.DangerousContent instead")]
        public const string DangerousContent = GeminiConstants.SafetyCategories.DangerousContent;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyCategories.Deceptive instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyCategories.Deceptive instead")]
        public const string Deceptive = GeminiConstants.SafetyCategories.Deceptive;
    }

    /// <summary>
    /// DEPRECATED: Use GeminiConstants.SafetyThresholds instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.SafetyThresholds instead. Will be removed in v7.0.0")]
    public static class SafetyThreshold
    {
        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyThresholds.Unspecified instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyThresholds.Unspecified instead")]
        public const string Unspecified = GeminiConstants.SafetyThresholds.Unspecified;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyThresholds.Low instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyThresholds.Low instead")]
        public const string Low = GeminiConstants.SafetyThresholds.Low;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyThresholds.Medium instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyThresholds.Medium instead")]
        public const string Medium = GeminiConstants.SafetyThresholds.Medium;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyThresholds.High instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyThresholds.High instead")]
        public const string High = GeminiConstants.SafetyThresholds.High;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyThresholds.None instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyThresholds.None instead")]
        public const string None = GeminiConstants.SafetyThresholds.None;
    }

    /// <summary>
    /// DEPRECATED: Use GeminiConstants.SafetyProbabilities instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.SafetyProbabilities instead. Will be removed in v7.0.0")]
    public static class SafetyProbability
    {
        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyProbabilities.Unspecified instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyProbabilities.Unspecified instead")]
        public const string Unspecified = GeminiConstants.SafetyProbabilities.Unspecified;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyProbabilities.Negligible instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyProbabilities.Negligible instead")]
        public const string Negligible = GeminiConstants.SafetyProbabilities.Negligible;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyProbabilities.Low instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyProbabilities.Low instead")]
        public const string Low = GeminiConstants.SafetyProbabilities.Low;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyProbabilities.Medium instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyProbabilities.Medium instead")]
        public const string Medium = GeminiConstants.SafetyProbabilities.Medium;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.SafetyProbabilities.High instead
        /// </summary>
        [Obsolete("Use GeminiConstants.SafetyProbabilities.High instead")]
        public const string High = GeminiConstants.SafetyProbabilities.High;
    }
}
