using Junaid.GoogleGemini.Net.Infrastructure.Utilities;

namespace Junaid.GoogleGemini.Net.Infrastructure.Constants
{
    /// <summary>
    /// DEPRECATED: Use GeminiConstants.Models instead
    /// Constants for available Gemini models
    /// </summary>
    [Obsolete("This class is deprecated. Use GeminiConstants.Models instead. Will be removed in v7.0.0")]
    public static class GeminiModels
    {
        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Gemini15Pro instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Gemini15Pro instead")]
        public const string Gemini15Pro = GeminiConstants.Models.Gemini15Pro;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Gemini15Flash instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Gemini15Flash instead")]
        public const string Gemini15Flash = GeminiConstants.Models.Gemini15Flash;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.GeminiPro instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.GeminiPro instead")]
        public const string GeminiPro = GeminiConstants.Models.GeminiPro;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Gemini10Pro instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Gemini10Pro instead")]
        public const string Gemini10Pro = GeminiConstants.Models.Gemini10Pro;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.GeminiProVision instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.GeminiProVision instead")]
        public const string GeminiProVision = GeminiConstants.Models.GeminiProVision;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Embedding001 instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Embedding001 instead")]
        public const string Embedding001 = GeminiConstants.Models.Embedding001;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.TextEmbedding004 instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.TextEmbedding004 instead")]
        public const string TextEmbedding004 = GeminiConstants.Models.TextEmbedding004;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.ContentGenerationModels instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.ContentGenerationModels instead")]
        public static readonly string[] ContentGenerationModels = GeminiConstants.Models.ContentGenerationModels;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.EmbeddingModels instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.EmbeddingModels instead")]
        public static readonly string[] EmbeddingModels = GeminiConstants.Models.EmbeddingModels;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.MultimodalModels instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.MultimodalModels instead")]
        public static readonly string[] MultimodalModels = GeminiConstants.Models.MultimodalModels;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Recommended instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Recommended instead")]
        public static string Recommended => GeminiConstants.Models.Recommended;

        /// <summary>
        /// DEPRECATED: Use GeminiConstants.Models.Fastest instead
        /// </summary>
        [Obsolete("Use GeminiConstants.Models.Fastest instead")]
        public static string Fastest => GeminiConstants.Models.Fastest;
    }
}