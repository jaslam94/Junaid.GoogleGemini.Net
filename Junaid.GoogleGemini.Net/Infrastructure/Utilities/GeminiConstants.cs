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
            /// GA (September 2, 2026), the most capable Flash model: "engineered for long-horizon
            /// software engineering, autonomous agents, and complex enterprise workflows" per Google's
            /// own description. It has the same introductory pricing as <see cref="Gemini36Flash"/>
            /// through December 31, 2026. It accepts the thinking levels low, medium and high, and
            /// returns HTTP 400 for minimal (live-verified). It ignores temperature, topP and topK (see
            /// the deprecation note on <c>GenerationConfig</c>).
            /// </summary>
            public const string Gemini38Flash = "gemini-3.8-flash";
            /// <summary>
            /// Deprecated by Google on October 8, 2026. Google now routes every request for this model
            /// to <see cref="Gemini38Flash"/>, so the response comes from 3.8 whatever name you send.
            /// Use <see cref="Gemini38Flash"/>.
            /// </summary>
            [Obsolete("Google deprecated gemini-3.7-flash on 2026-10-08 and routes it to gemini-3.8-flash. Use Models.Gemini38Flash.")]
            public const string Gemini37Flash = "gemini-3.7-flash";
            /// <summary>
            /// GA (July 2026): improved coding and agentic planning, and about 17% fewer tokens per
            /// response than gemini-3.5-flash. Superseded as the default by <see cref="Gemini38Flash"/>
            /// but still supported, with the same introductory pricing through December 31, 2026. Per
            /// Google's docs it accepts all four thinking levels (we did not test that). It ignores
            /// temperature, topP and topK (see the deprecation note on <c>GenerationConfig</c>).
            /// </summary>
            public const string Gemini36Flash = "gemini-3.6-flash";
            /// <summary>
            /// Deprecated by Google on October 8, 2026. Google now routes every request for this model
            /// to <see cref="Gemini36Flash"/>. Use <see cref="Gemini38Flash"/> or
            /// <see cref="Gemini36Flash"/>.
            /// </summary>
            [Obsolete("Google deprecated gemini-3.5-flash on 2026-10-08 and routes it to gemini-3.6-flash. Use Models.Gemini38Flash or Models.Gemini36Flash.")]
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
            /// <summary>
            /// GA (October 6, 2026): Nano Banana 2.1, the update to <see cref="Gemini31FlashImage"/>.
            /// It improves text rendering and multi-turn character consistency. It accepts the wide and
            /// tall ratios 1:4, 4:1, 1:8 and 8:1 (live-verified at 1:8), and the sizes 1K, 2K and 4K. It
            /// rejects the 512 size. Thinking levels are minimal, medium (the default) and high. It has
            /// no free tier and does not support context caching. Live-tested 2026-10-10: simple prompts
            /// sometimes return <c>finishReason: IMAGE_RECITATION</c> with no image, so
            /// <see cref="RecommendedImage"/> still points at the older model.
            /// </summary>
            public const string GeminiNanoBanana21 = "gemini-nano-banana-2.1";
            /// <summary>
            /// Highest-quality native image generation (Nano Banana Pro). GA (May 2026, unsuffixed
            /// name); the earlier "-preview"-suffixed model ID this constant used to hold was shut
            /// down June 25, 2026. Pass this value, not the old preview string.
            /// </summary>
            public const string Gemini3ProImage = "gemini-3-pro-image";
            /// <summary>
            /// High-efficiency native image generation (Nano Banana 2). GA (May 2026, unsuffixed
            /// name); the earlier "-preview"-suffixed model ID this constant used to hold was shut
            /// down June 25, 2026. Pass this value, not the old preview string. Google deprecated this
            /// model on October 6, 2026 (no shutdown date yet) in favor of <see cref="GeminiNanoBanana21"/>.
            /// </summary>
            public const string Gemini31FlashImage = "gemini-3.1-flash-image";
            /// <summary>
            /// GA (June 2026), ultra-low-latency native image generation (Nano Banana 2 Lite): the
            /// fastest and most cost-effective image model, for high-volume or latency-sensitive use.
            /// </summary>
            public const string Gemini31FlashLiteImage = "gemini-3.1-flash-lite-image";

            // Text-to-speech. The two 3.8 models are GA (September 22, 2026) and live-verified, see
            // PLAN-tts-3-8.md. The three older models shut down on November 17, 2026. This library does
            // not enumerate voice names (e.g. "Kore") as constants: see PLAN-tts.md for why, and Google's
            // TTS voice list for the current full set.

            /// <summary>
            /// GA (September 22, 2026): Google's flagship TTS model, built for studio-grade voice, acting
            /// nuance and long-form audio. It covers over 130 languages. It reads the prompt as a verbatim
            /// transcript, so put delivery directions in <c>SpeechStyle</c>, not in the text. Input is
            /// limited to 8,192 tokens. It has a free tier.
            /// </summary>
            public const string Gemini38FlashTts = "gemini-3.8-flash-tts";
            /// <summary>
            /// GA (September 22, 2026): the faster, cheaper TTS model for high-volume and real-time use.
            /// It is Google's replacement for <c>gemini-3.1-flash-tts-preview</c> and covers over 100
            /// languages. It reads the prompt as a verbatim transcript, like <see cref="Gemini38FlashTts"/>.
            /// It has a free tier.
            /// </summary>
            public const string Gemini38FlashLiteTts = "gemini-3.8-flash-lite-tts";
            /// <summary>Shuts down November 17, 2026. Use <see cref="Gemini38FlashLiteTts"/> or <see cref="Gemini38FlashTts"/>.</summary>
            [Obsolete("Google shuts down gemini-2.5-flash-preview-tts on 2026-11-17. Use Models.Gemini38FlashLiteTts or Models.Gemini38FlashTts.")]
            public const string Gemini25FlashTts = "gemini-2.5-flash-preview-tts";
            /// <summary>Shuts down November 17, 2026. Use <see cref="Gemini38FlashTts"/>.</summary>
            [Obsolete("Google shuts down gemini-2.5-pro-preview-tts on 2026-11-17. Use Models.Gemini38FlashTts or Models.Gemini38FlashLiteTts.")]
            public const string Gemini25ProTts = "gemini-2.5-pro-preview-tts";
            /// <summary>Shuts down November 17, 2026. Use <see cref="Gemini38FlashLiteTts"/>, Google's named replacement.</summary>
            [Obsolete("Google shuts down gemini-3.1-flash-tts-preview on 2026-11-17. Use Models.Gemini38FlashLiteTts or Models.Gemini38FlashTts.")]
            public const string Gemini31FlashTts = "gemini-3.1-flash-tts-preview";

            // Speech-to-text. This is NOT a generateContent model: it is called through the
            // Interactions API (see PLAN-stt.md), a different endpoint with its own request/response
            // shape. Do not pass this to GenerateAsync or any other generateContent-based method; use
            // ITranscriptionService instead. GA (August 26, 2026), live-verified request/response
            // shape, see PLAN-stt.md. Not listed in ContentGenerationModels for that reason.
            public const string Gemini35Transcribe = "gemini-3.5-transcribe";

            // Gemini 2.x. Not deprecated, but since September 18, 2026 Google limits access to projects
            // that used these models before. A new project can be refused. Use the Gemini 3 models above.
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
                Gemini38Flash,
                Gemini36Flash,
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
            public static string Recommended => Gemini38Flash;

            /// <summary>The fastest / most cost-efficient model (GA).</summary>
            public static string Fastest => Gemini35FlashLite;

            /// <summary>
            /// The recommended model for image generation: the efficient flash variant, not the
            /// higher-cost "pro" one. This is still <see cref="Gemini31FlashImage"/>, which Google
            /// deprecated on October 6, 2026 without a shutdown date. We tried
            /// <see cref="GeminiNanoBanana21"/> as the default on 2026-10-10 and kept the old model:
            /// the newer one answered simple prompts with <c>finishReason: IMAGE_RECITATION</c> and no
            /// image ("A small red circle on a plain white background" failed 2 of 2 calls, and a plain
            /// blue rectangle failed 1 of 6), while the old model returned an image. Pass
            /// <see cref="GeminiNanoBanana21"/> explicitly to use it, and check
            /// <c>GenerateContentResponse.FinishReason</c> when no image comes back. Pass
            /// <see cref="Gemini3ProImage"/> for higher quality, or <see cref="Gemini31FlashLiteImage"/>
            /// for the fastest and cheapest option. Change this default once Google sets a shutdown
            /// date for the old model.
            /// </summary>
            public static string RecommendedImage => Gemini31FlashImage;

            /// <summary>
            /// The recommended model for text-to-speech: <see cref="Gemini38FlashLiteTts"/>, the
            /// cheaper of the two 3.8 models. It has a free tier on Google's Standard rate. Pass
            /// <see cref="Gemini38FlashTts"/> explicitly for the highest voice quality. Before 6.8.0
            /// this was <c>gemini-2.5-flash-preview-tts</c>, which shuts down on November 17, 2026.
            /// </summary>
            public static string RecommendedTts => Gemini38FlashLiteTts;

            /// <summary>
            /// The recommended model for speech-to-text (only one option exists today, see
            /// <see cref="Gemini35Transcribe"/>). Called through the Interactions API, not
            /// <c>generateContent</c>; use <see cref="Junaid.GoogleGemini.Net.Services.Interfaces.ITranscriptionService"/>.
            /// </summary>
            public static string RecommendedTranscription => Gemini35Transcribe;
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
        /// Task types for embeddings. This tells the model how the vector will be used so it can optimize it.
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
            /// "thinking" models) can take well over a minute on a single call at a high reasoning
            /// depth; a short timeout produces spurious GeminiTimeoutExceptions.
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
            public const string Model = Models.Gemini38Flash;
        }

        #endregion Default Values

        #region Reasoning & Media

        /// <summary>
        /// Thinking levels for Gemini 3+ (<c>thinkingConfig.thinkingLevel</c>). Controls reasoning depth.
        /// Not every model accepts every level. Live-verified on 2026-10-10: <c>gemini-3.8-flash</c>
        /// accepts low, medium and high, and returns HTTP 400 for <see cref="Minimal"/>. Per Google,
        /// <c>gemini-3.6-flash</c> and <c>gemini-3.5-flash-lite</c> accept all four levels, and
        /// <c>gemini-nano-banana-2.1</c> accepts minimal, medium and high. To cut thinking on
        /// <c>gemini-3.8-flash</c>, <c>GeminiRequestOptions.ThinkingBudget = 0</c> was accepted (HTTP 200)
        /// and usually produced no thought tokens, but one call in eight still thought. Google's docs do not
        /// describe that, so treat it as observed behavior, not a guarantee.
        /// </summary>
        public static class ThinkingLevels
        {
            /// <summary>Not supported by <c>gemini-3.8-flash</c> (HTTP 400).</summary>
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

        #region Image Generation

        /// <summary>
        /// Output modalities for <c>generationConfig.responseModalities</c>. Set <c>[Text, Image]</c>
        /// (or just <c>[Image]</c> on models that allow image-only output) to get generated images back
        /// as <c>inlineData</c> parts, see <c>GenerateContentResponse.Images()</c>. Set <c>[Audio]</c>
        /// on a TTS model to get generated speech back the same way, see
        /// <c>GenerateContentResponse.Audio()</c>.
        /// </summary>
        public static class ResponseModalities
        {
            public const string Text = "TEXT";
            public const string Image = "IMAGE";
            public const string Audio = "AUDIO";
        }

        /// <summary>
        /// Aspect ratios for <c>generationConfig.imageConfig.aspectRatio</c> (Gemini 3+ image models).
        /// </summary>
        public static class ImageAspectRatios
        {
            public const string Square = "1:1";
            public const string Landscape3x2 = "3:2";
            public const string Portrait2x3 = "2:3";
            public const string Portrait3x4 = "3:4";
            public const string Landscape4x3 = "4:3";
            public const string Portrait4x5 = "4:5";
            public const string Landscape5x4 = "5:4";
            public const string Portrait9x16 = "9:16";
            public const string Widescreen16x9 = "16:9";
            public const string Ultrawide21x9 = "21:9";

            /// <summary>Tall strip. Listed in Google's API reference for <c>gemini-nano-banana-2.1</c> (GA October 2026).</summary>
            public const string Tall1x4 = "1:4";
            /// <summary>Wide strip. Added with <c>gemini-nano-banana-2.1</c> (GA October 2026).</summary>
            public const string Wide4x1 = "4:1";
            /// <summary>Very tall strip. Live-verified on <c>gemini-nano-banana-2.1</c> (GA October 2026).</summary>
            public const string Tall1x8 = "1:8";
            /// <summary>Very wide strip. Added with <c>gemini-nano-banana-2.1</c> (GA October 2026).</summary>
            public const string Wide8x1 = "8:1";
        }

        /// <summary>
        /// Resolutions for <c>generationConfig.imageConfig.imageSize</c> (Gemini 3+ image models).
        /// </summary>
        public static class ImageSizes
        {
            /// <summary>
            /// The API accepts <c>512</c> as a value, and Google documents it for
            /// <c>gemini-3.1-flash-image</c>. <c>gemini-nano-banana-2.1</c> rejects it with HTTP 400
            /// ("Image size 512 is not supported for this model"), live-verified on 2026-10-10.
            /// </summary>
            public const string FiveTwelve = "512";
            public const string OneK = "1K";
            public const string TwoK = "2K";
            public const string FourK = "4K";
        }

        #endregion Image Generation

        #region Audio & Video

        /// <summary>
        /// Output encodings for <c>generationConfig.responseFormat.audio.mimeType</c> on the Gemini 3.8
        /// TTS models. Live-verified on 2026-10-10: without this setting a unary call returns
        /// <c>audio/wav</c> and a streaming call returns <c>audio/l16</c>.
        /// </summary>
        public static class AudioFormats
        {
            /// <summary>
            /// Raw 16-bit linear PCM, returned as <c>audio/l16; rate=24000; channels=1</c>. This is the
            /// shape <c>GeneratedAudio.ToWav()</c> wraps in a WAV header.
            /// </summary>
            public const string Pcm16 = "AUDIO_L16";
            /// <summary>A WAV file with a RIFF header. This is the default for a unary 3.8 TTS call.</summary>
            public const string Wav = "AUDIO_WAV";
            /// <summary>8-bit G.711 mu-law, for telephony. <c>ToWav()</c> does not convert this format.</summary>
            public const string Mulaw = "AUDIO_MULAW";
            /// <summary>8-bit G.711 A-law, for telephony. <c>ToWav()</c> does not convert this format.</summary>
            public const string Alaw = "AUDIO_ALAW";
        }

        /// <summary>
        /// Values for <c>Part.MediaProcessing</c>, which controls how the model reads a video part.
        /// Both values were live-tested on 2026-10-10 against short clips. See <c>Part.MediaProcessing</c>
        /// for what we saw.
        /// </summary>
        public static class MediaProcessing
        {
            /// <summary>Fixed-rate frame extraction. All frames go into the context.</summary>
            public const string Static = "STATIC";
            /// <summary>
            /// The model navigates the video on demand and asks for transcripts, frames or audio as it
            /// needs them. Google calls this "recommended for most use cases" and claims up to 88%
            /// fewer tokens on long videos. On two 10-second clips it used slightly more, and it bills
            /// the loaded media as tool-use tokens (<c>UsageMetadata.ToolUsePromptTokenCount</c>).
            /// </summary>
            public const string Agentic = "AGENTIC";
        }

        #endregion Audio & Video
    }
}