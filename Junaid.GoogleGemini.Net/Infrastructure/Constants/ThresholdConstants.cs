using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Constants
{
    /// <summary>
    /// DEPRECATED: Use GeminiConstants.SafetyThresholds instead
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.SafetyThresholds instead. Will be removed in v7.0.0")]
    public static class ThresholdConstants
    {
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
}