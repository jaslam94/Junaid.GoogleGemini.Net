namespace Junaid.GoogleGemini.Net.Infrastructure.Utilities
{
    /// <summary>
    /// Consolidated constants for the Gemini API
    /// </summary>
    public static class GeminiConstants
    {
        #region API Configuration

        /// <summary>
        /// Default Gemini API base URL
        /// </summary>
        public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1/";

        /// <summary>
        /// Environment variable name for API key
        /// </summary>
        public const string ApiKeyEnvironmentVariable = "GeminiApiKey";

        /// <summary>
        /// HTTP header name for API key authentication
        /// </summary>
        public const string ApiKeyHeaderName = "x-goog-api-key";

        #endregion API Configuration

        #region Model Information

        /// <summary>
        /// Constants for available Gemini models
        /// </summary>
        public static class Models
        {
            public const string Gemini25Pro = "gemini-2.5-pro";
            public const string Gemini25Flash = "gemini-2.5-flash";
            public const string Gemini20Flash = "gemini-2.0-flash-001";
            public const string Gemini15Pro = "gemini-1.5-pro";
            public const string Gemini15Flash = "gemini-1.5-flash";

            [Obsolete("Use newer models; this variant may no longer be available.")]
            public const string GeminiProVision = "gemini-pro-vision";

            public const string GeminiPro = "gemini-pro";
            public const string Gemini10Pro = "gemini-1.0-pro";
            public const string Embedding001 = "embedding-001";
            public const string TextEmbedding004 = "text-embedding-004";

            public static readonly string[] ContentGenerationModels =
            {
                Gemini25Pro,
                Gemini25Flash,
                Gemini20Flash,
                Gemini15Pro,
                Gemini15Flash,
                GeminiPro,
                Gemini10Pro
            };

            public static readonly string[] EmbeddingModels =
            {
                Embedding001,
                TextEmbedding004
            };

            public static readonly string[] MultimodalModels =
            {
                Gemini25Pro,
                Gemini25Flash,
                Gemini20Flash,
                Gemini15Pro,
                Gemini15Flash
            };

            public static string Recommended => Gemini25Pro;
            public static string Fastest => Gemini15Flash; // Changed from non-existent lite model to actual fast model
        }

        #endregion Model Information

        #region Safety Configuration

        /// <summary>
        /// Constants for safety categories in the Gemini API
        /// </summary>
        public static class SafetyCategories
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
            /// Gets all valid safety categories supported by the Gemini API
            /// </summary>
            public static readonly string[] All =
            {
                Harassment,
                HateSpeech,
                SexuallyExplicit,
                DangerousContent
            };
        }

        /// <summary>
        /// Constants for safety thresholds in the Gemini API
        /// </summary>
        public static class SafetyThresholds
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
        public static class SafetyProbabilities
        {
            public const string Unspecified = "HARM_PROBABILITY_UNSPECIFIED";
            public const string Negligible = "NEGLIGIBLE";
            public const string Low = "LOW";
            public const string Medium = "MEDIUM";
            public const string High = "HIGH";

            /// <summary>
            /// Gets all probability levels in order from lowest to highest
            /// </summary>
            public static readonly string[] InOrder =
            {
                Negligible,
                Low,
                Medium,
                High
            };
        }

        #endregion Safety Configuration

        #region API Limits

        /// <summary>
        /// API limits and constraints
        /// </summary>
        public static class Limits
        {
            /// <summary>
            /// Maximum text input length for most models
            /// </summary>
            public const int MaxTextLength = 100000;

            /// <summary>
            /// Maximum image file size (20MB)
            /// </summary>
            public const int MaxImageSize = 20 * 1024 * 1024;

            /// <summary>
            /// Maximum number of messages in chat history
            /// </summary>
            public const int MaxChatMessages = 50;

            /// <summary>
            /// Maximum length per chat message
            /// </summary>
            public const int MaxMessageLength = 10000;

            /// <summary>
            /// Default rate limit requests per minute
            /// </summary>
            public const int DefaultRequestsPerMinute = 60;

            /// <summary>
            /// Default rate limit tokens per minute
            /// </summary>
            public const int DefaultTokensPerMinute = 60000;

            /// <summary>
            /// Maximum batch size for embedding requests
            /// </summary>
            public const int MaxEmbeddingBatchSize = 100;

            /// <summary>
            /// Maximum text length for embedding requests
            /// </summary>
            public const int MaxEmbeddingTextLength = 20000;
        }

        #endregion API Limits

        #region Default Values

        /// <summary>
        /// Default values for various configuration options
        /// </summary>
        public static class Defaults
        {
            /// <summary>
            /// Default timeout in seconds
            /// </summary>
            public const int TimeoutSeconds = 30;

            /// <summary>
            /// Default maximum retries
            /// </summary>
            public const int MaxRetries = 3;

            /// <summary>
            /// Default temperature for content generation
            /// </summary>
            public const float Temperature = 0.7f;

            /// <summary>
            /// Default model for new requests
            /// </summary>
            public const string Model = Models.Gemini15Pro;
        }

        #endregion Default Values
    }
}