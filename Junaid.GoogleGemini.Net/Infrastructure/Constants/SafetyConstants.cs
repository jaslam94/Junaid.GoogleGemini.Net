namespace Junaid.GoogleGemini.Net.Infrastructure.Constants
{
    /// <summary>
    /// Constants for safety categories in the Gemini API
    /// </summary>
    public static class SafetyCategory
    {
        /// <summary>
        /// Content intended to harass, bully, or abuse
        /// </summary>
        public const string Harassment = "HARM_CATEGORY_HARASSMENT";

        /// <summary>
        /// Content expressing hate, bias, or identity attacks
        /// </summary>
        public const string HateSpeech = "HARM_CATEGORY_HATE_SPEECH";

        /// <summary>
        /// Sexually explicit content
        /// </summary>
        public const string SexuallyExplicit = "HARM_CATEGORY_SEXUALLY_EXPLICIT";

        /// <summary>
        /// Content promoting dangerous or illegal activities
        /// </summary>
        public const string DangerousContent = "HARM_CATEGORY_DANGEROUS_CONTENT";

        /// <summary>
        /// Deceptive or manipulative content
        /// </summary>
        public const string Deceptive = "HARM_CATEGORY_DECEPTIVE";
    }

    /// <summary>
    /// Constants for safety thresholds in the Gemini API
    /// </summary>
    public static class SafetyThreshold
    {
        /// <summary>
        /// Block content if probability of category exceeds a very low threshold
        /// </summary>
        public const string Unspecified = "HARM_BLOCK_THRESHOLD_UNSPECIFIED";

        /// <summary>
        /// Block content with low probability of matching safety category
        /// </summary>
        public const string Low = "BLOCK_LOW_AND_ABOVE";

        /// <summary>
        /// Block content with medium or higher probability of matching safety category
        /// </summary>
        public const string Medium = "BLOCK_MEDIUM_AND_ABOVE";

        /// <summary>
        /// Block only content with high probability of matching safety category
        /// </summary>
        public const string High = "BLOCK_ONLY_HIGH";

        /// <summary>
        /// Do not block any content based on safety category
        /// </summary>
        public const string None = "BLOCK_NONE";
    }

    /// <summary>
    /// Constants for safety rating probabilities in the Gemini API responses
    /// </summary>
    public static class SafetyProbability
    {
        public const string Unspecified = "HARM_PROBABILITY_UNSPECIFIED";
        public const string Negligible = "NEGLIGIBLE";
        public const string Low = "LOW";
        public const string Medium = "MEDIUM";
        public const string High = "HIGH";
    }
}
