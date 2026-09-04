using Junaid.GoogleGemini.Net.Models.GoogleApi;
using Junaid.GoogleGemini.Net.Infrastructure.Utilities;
using System.Text.Json.Nodes;

namespace Junaid.GoogleGemini.Net.Models.Requests
{
    /// <summary>
    /// Simple options class for configuring Gemini requests
    /// </summary>
    public class GeminiRequestOptions
    {
        /// <summary>
        /// Controls randomness in generation (0.0 to 1.0). <b>Ignored on <c>gemini-3.8-flash</c>,
        /// <c>gemini-3.7-flash</c>, <c>gemini-3.6-flash</c>, <c>gemini-3.5-flash-lite</c> and later</b>. Google deprecated all
        /// sampling params on those models (July-August 2026) in favor of steering determinism via
        /// <see cref="SystemInstruction"/>.
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Top-p sampling parameter. See the deprecation note on <see cref="Temperature"/>.
        /// </summary>
        public float? TopP { get; set; }

        /// <summary>
        /// Top-k sampling parameter. See the deprecation note on <see cref="Temperature"/>.
        /// </summary>
        public int? TopK { get; set; }

        /// <summary>
        /// Model to use for generation (overrides default)
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Custom safety settings for this request
        /// </summary>
        public List<SafetySetting>? SafetySettings { get; set; }

        /// <summary>
        /// Stop sequences for generation
        /// </summary>
        public List<string>? StopSequences { get; set; }

        /// <summary>
        /// System-level instruction applied to the request (persona, tone, rules). Text only.
        /// </summary>
        public string? SystemInstruction { get; set; }

        /// <summary>
        /// Output MIME type (e.g. <c>application/json</c> for structured output). When using
        /// <c>GenerateAsync&lt;T&gt;</c> this is set for you.
        /// </summary>
        public string? ResponseMimeType { get; set; }

        /// <summary>A response schema (OpenAPI subset) the JSON output must conform to.</summary>
        public JsonNode? ResponseSchema { get; set; }

        /// <summary>
        /// Thinking/reasoning token budget (Gemini 2.5). <c>0</c> disables thinking where allowed;
        /// <c>-1</c> lets the model decide. Mutually exclusive with <see cref="ThinkingLevel"/>.
        /// </summary>
        public int? ThinkingBudget { get; set; }

        /// <summary>
        /// Reasoning depth for Gemini 3+ (see <c>GeminiConstants.ThinkingLevels</c>): "minimal", "low",
        /// "medium", or "high". Mutually exclusive with <see cref="ThinkingBudget"/>.
        /// </summary>
        public string? ThinkingLevel { get; set; }

        /// <summary>Media resolution for image/video/PDF parts (see <c>GeminiConstants.MediaResolutions</c>).</summary>
        public string? MediaResolution { get; set; }

        /// <summary>When true, includes thought-summary parts in the response.</summary>
        public bool? IncludeThoughts { get; set; }

        /// <summary>Deterministic sampling seed.</summary>
        public int? Seed { get; set; }

        /// <summary>Number of candidate responses to generate.</summary>
        public int? CandidateCount { get; set; }

        /// <summary>
        /// Explicit tools to send. If null, tools are assembled from <see cref="Functions"/> and the
        /// Enable* flags below.
        /// </summary>
        public List<Tool>? Tools { get; set; }

        /// <summary>Function-calling configuration (mode / allow-list).</summary>
        public ToolConfig? ToolConfig { get; set; }

        /// <summary>Functions the model may call.</summary>
        public List<FunctionDeclaration>? Functions { get; set; }

        /// <summary>Enable grounding with Google Search.</summary>
        public bool EnableGoogleSearch { get; set; }

        /// <summary>Enable the URL-context tool.</summary>
        public bool EnableUrlContext { get; set; }

        /// <summary>Enable server-side code execution.</summary>
        public bool EnableCodeExecution { get; set; }

        /// <summary>
        /// Name of a cached-content resource (e.g. <c>cachedContents/abc</c>) to reuse for this request.
        /// </summary>
        public string? CachedContent { get; set; }

        /// <summary>
        /// Output modalities to request (see <c>GeminiConstants.ResponseModalities</c>). Set by
        /// <c>GenerateImageAsync</c> automatically when unset; set it yourself for full control.
        /// </summary>
        public List<string>? ResponseModalities { get; set; }

        /// <summary>Image aspect ratio (see <c>GeminiConstants.ImageAspectRatios</c>), Gemini 3+ image models.</summary>
        public string? ImageAspectRatio { get; set; }

        /// <summary>Image resolution (see <c>GeminiConstants.ImageSizes</c>), Gemini 3+ image models.</summary>
        public string? ImageSize { get; set; }

        /// <summary>
        /// Creates options optimized for creative tasks using the recommended model
        /// </summary>
        public static GeminiRequestOptions Creative(string? model = null) => new()
        {
            Temperature = 0.9f,
            TopP = 0.8f,
            TopK = 40,
            Model = model ?? GeminiConstants.Models.Recommended,
            SafetySettings = new List<SafetySetting>
            {
                new() { Category = GeminiConstants.SafetyCategories.Harassment, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.HateSpeech, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.SexuallyExplicit, Threshold = GeminiConstants.SafetyThresholds.High },
                new() { Category = GeminiConstants.SafetyCategories.DangerousContent, Threshold = GeminiConstants.SafetyThresholds.High }
            }
        };

        /// <summary>
        /// Creates options optimized for factual/precise tasks using the recommended model.
        /// <b>Note:</b> the recommended model (<c>gemini-3.8-flash</c>) ignores these sampling params
        /// (see the deprecation note on <see cref="Temperature"/>); set <see cref="SystemInstruction"/>
        /// with explicit determinism rules for that model instead.
        /// </summary>
        public static GeminiRequestOptions Factual(string? model = null) => new()
        {
            Temperature = 0.1f,
            TopP = 0.1f,
            TopK = 1,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates options optimized for code generation using the recommended model.
        /// <b>Note:</b> the recommended model (<c>gemini-3.8-flash</c>) ignores these sampling params
        /// (see the deprecation note on <see cref="Temperature"/>); set <see cref="SystemInstruction"/>
        /// with explicit determinism rules for that model instead.
        /// </summary>
        public static GeminiRequestOptions Code(string? model = null) => new()
        {
            Temperature = 0.1f,
            TopP = 0.1f,
            TopK = 1,
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates default balanced options using the recommended model
        /// </summary>
        public static GeminiRequestOptions Default(string? model = null) => new()
        {
            // No explicit temperature: let the model use its native default (1.0 on Gemini 3, which
            // recommends against lowering it).
            Model = model ?? GeminiConstants.Models.Recommended
        };

        /// <summary>
        /// Creates options optimized for fast responses using the fastest model
        /// </summary>
        public static GeminiRequestOptions Fast() => new()
        {
            Model = GeminiConstants.Models.Fastest
        };

        /// <summary>
        /// Creates options with a specific model
        /// </summary>
        /// <param name="model">The specific model to use</param>
        /// <param name="temperature">Optional temperature setting</param>
        public static GeminiRequestOptions WithModel(string model, float? temperature = null) => new()
        {
            Model = model,
            Temperature = temperature
        };

        /// <summary>
        /// Creates a copy of current options with a different model
        /// </summary>
        /// <param name="model">The model to use</param>
        public GeminiRequestOptions UseModel(string model)
        {
            var copy = Clone();
            copy.Model = model;
            return copy;
        }

        /// <summary>Creates a shallow copy of these options (used to avoid mutating a caller's instance).</summary>
        public GeminiRequestOptions Clone() => new()
        {
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            TopP = TopP,
            TopK = TopK,
            Model = Model,
            SafetySettings = SafetySettings,
            StopSequences = StopSequences,
            SystemInstruction = SystemInstruction,
            ResponseMimeType = ResponseMimeType,
            ResponseSchema = ResponseSchema,
            ThinkingBudget = ThinkingBudget,
            ThinkingLevel = ThinkingLevel,
            MediaResolution = MediaResolution,
            IncludeThoughts = IncludeThoughts,
            Seed = Seed,
            CandidateCount = CandidateCount,
            Tools = Tools,
            ToolConfig = ToolConfig,
            Functions = Functions,
            EnableGoogleSearch = EnableGoogleSearch,
            EnableUrlContext = EnableUrlContext,
            EnableCodeExecution = EnableCodeExecution,
            CachedContent = CachedContent,
            ResponseModalities = ResponseModalities,
            ImageAspectRatio = ImageAspectRatio,
            ImageSize = ImageSize
        };
    }
}