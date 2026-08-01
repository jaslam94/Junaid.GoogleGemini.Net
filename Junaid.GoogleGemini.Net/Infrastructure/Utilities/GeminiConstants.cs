namespace Junaid.GoogleGemini.Net.Infrastructure.Utilities
{
    /// <summary>
    /// Consolidated constants for the Gemini API
    /// </summary>
    public static class GeminiConstants
    {
        #region API Configuration

        /// <summary>
        /// Default Gemini API base URL. Uses v1beta, where the modern features live (structured
        /// output, thinking config, grounding, context caching, the Files API, etc.).
        /// </summary>
        public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/";

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
            // Gemini 3 family (current). These names are informational; model names are not validated
            // against any allow-list, so newer models work without a library update.
            /// <summary>
            /// GA (July 2026), current workhorse model: improved coding/agentic planning and ~17%
            /// fewer tokens per response than <see cref="Gemini35Flash"/>. See the temperature/topP/topK
            /// deprecation note on <c>GenerationConfig</c> — this model ignores those sampling params.
            /// </summary>
            public const string Gemini36Flash = "gemini-3.6-flash";
            /// <summary>GA, sustained frontier flash performance. Superseded as the default by <see cref="Gemini36Flash"/> (July 2026) but still fully supported.</summary>
            public const string Gemini35Flash = "gemini-3.5-flash";
            public const string Gemini31Pro = "gemini-3.1-pro-preview";
            public const string Gemini3Flash = "gemini-3-flash-preview";
            /// <summary>
            /// GA (July 2026), the most cost-effective model in the 3.5 class. Also ignores
            /// temperature/topP/topK (see <see cref="Gemini36Flash"/>).
            /// </summary>
            public const string Gemini35FlashLite = "gemini-3.5-flash-lite";
            /// <summary>GA, cost-efficient for high volume.</summary>
            public const string Gemini31FlashLite = "gemini-3.1-flash-lite";
            /// <summary>Highest-quality native image generation (Nano Banana).</summary>
            public const string Gemini3ProImage = "gemini-3-pro-image-preview";
            /// <summary>High-efficiency native image generation (Nano Banana).</summary>
            public const string Gemini31FlashImage = "gemini-3.1-flash-image-preview";

            // Gemini 2.x (still available).
            public const string Gemini25Pro = "gemini-2.5-pro";
            public const string Gemini25Flash = "gemini-2.5-flash";
            public const string Gemini15Pro = "gemini-1.5-pro";
            public const string Gemini15Flash = "gemini-1.5-flash";
            public const string GeminiPro = "gemini-pro";
            public const string Gemini10Pro = "gemini-1.0-pro";

            // Embeddings.
            public const string Embedding001 = "embedding-001";
            public const string TextEmbedding004 = "text-embedding-004";
            /// <summary>General-purpose text embedding model.</summary>
            public const string GeminiEmbedding001 = "gemini-embedding-001";
            /// <summary>Multimodal embedding model (text, image, video, audio, PDF).</summary>
            public const string GeminiEmbedding2 = "gemini-embedding-2";

            /// <summary>A non-exhaustive list of common content-generation models (informational).</summary>
            public static readonly string[] ContentGenerationModels =
            {
                Gemini36Flash,
                Gemini35Flash,
                Gemini31Pro,
                Gemini3Flash,
                Gemini35FlashLite,
                Gemini31FlashLite,
                Gemini25Pro,
                Gemini25Flash
            };

            /// <summary>A non-exhaustive list of common embedding models (informational).</summary>
            public static readonly string[] EmbeddingModels =
            {
                GeminiEmbedding2,
                GeminiEmbedding001,
                TextEmbedding004,
                Embedding001
            };

            /// <summary>The recommended general-purpose model (GA).</summary>
            public static string Recommended => Gemini36Flash;

            /// <summary>The fastest / most cost-efficient model (GA).</summary>
            public static string Fastest => Gemini35FlashLite;
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
            /// Use the default threshold for this safety category (threshold determined by the model)
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

        #region Embeddings

        /// <summary>
        /// Task types for embeddings — tells the model how the vector will be used so it can optimize it.
        /// </summary>
        public static class EmbeddingTaskTypes
        {
            public const string RetrievalQuery = "RETRIEVAL_QUERY";
            public const string RetrievalDocument = "RETRIEVAL_DOCUMENT";
            public const string SemanticSimilarity = "SEMANTIC_SIMILARITY";
            public const string Classification = "CLASSIFICATION";
            public const string Clustering = "CLUSTERING";
            public const string QuestionAnswering = "QUESTION_ANSWERING";
            public const string FactVerification = "FACT_VERIFICATION";
            public const string CodeRetrievalQuery = "CODE_RETRIEVAL_QUERY";
        }

        #endregion Embeddings

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
            /// Default timeout in seconds. Generous by default because current models (Gemini 3
            /// "thinking" models default to high reasoning depth) can take well over a minute on a
            /// single call; a short timeout produces spurious GeminiTimeoutExceptions.
            /// </summary>
            public const int TimeoutSeconds = 100;

            /// <summary>
            /// Default maximum retries
            /// </summary>
            public const int MaxRetries = 3;

            /// <summary>
            /// Default temperature for content generation
            /// </summary>
            public const float Temperature = 0.7f;

            /// <summary>
            /// Default model for new requests (a current GA model).
            /// </summary>
            public const string Model = Models.Gemini36Flash;
        }

        #endregion Default Values

        #region Reasoning & Media

        /// <summary>
        /// Thinking levels for Gemini 3+ (<c>thinkingConfig.thinkingLevel</c>). Controls reasoning depth.
        /// </summary>
        public static class ThinkingLevels
        {
            public const string Minimal = "minimal";
            public const string Low = "low";
            public const string Medium = "medium";
            public const string High = "high";
        }

        /// <summary>
        /// Media resolution settings for image/video/PDF parts (Gemini 3+).
        /// </summary>
        public static class MediaResolutions
        {
            public const string Low = "media_resolution_low";
            public const string Medium = "media_resolution_medium";
            public const string High = "media_resolution_high";
            public const string UltraHigh = "media_resolution_ultra_high";
        }

        #endregion Reasoning & Media
    }
}